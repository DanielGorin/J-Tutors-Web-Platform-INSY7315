﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace J_Tutors_Web_Platform.Services
{
    /// <summary>
    /// User booking flow using ADO.NET (no EF).
    /// Tables used:
    ///  - Subjects(SubjectID, SubjectName, IsActive, ...)
    ///  - PricingRule(PricingRuleID, SubjectID, HourlyRate, MinHours, MaxHours, MaxPointDiscount, ...)
    ///  - AvailabilityBlock(AvailabilityBlockID, AdminID, BlockDate, StartTime, EndTime)
    ///  - TutoringSession(..., UserID, AdminID, SubjectID, SessionDate, StartTime, DurationHours, BaseCost, PointsSpent, Status, ...)
    /// </summary>
    public sealed class UserBookingService
    {
        private readonly IConfiguration _config;
        private readonly AdminAgendaService _agenda;
        private const string ConnName = "AzureSql";

        public UserBookingService(IConfiguration config, AdminAgendaService agenda)
        {
            _config = config;
            _agenda = agenda;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Connection helpers
        // ─────────────────────────────────────────────────────────────────────────────
        private string GetConnectionString()
            => _config.GetConnectionString(ConnName)
               ?? throw new InvalidOperationException($"Connection string '{ConnName}' not found.");

        private async Task<SqlConnection> OpenAsync()
        {
            var con = new SqlConnection(GetConnectionString());
            await con.OpenAsync();
            return con;
        }

        private static SqlCommand Cmd(SqlConnection con, string sql, IDictionary<string, object?>? p = null)
        {
            var cmd = new SqlCommand(sql, con) { CommandType = CommandType.Text };
            if (p != null)
                foreach (var kv in p)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
            return cmd;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Subjects (dropdown)
        // ─────────────────────────────────────────────────────────────────────────────
        public IReadOnlyList<SubjectListItemVM> GetSubjectsForBooking()
        {
            const string sql = @"
SELECT SubjectID, SubjectName
FROM Subjects
WHERE IsActive = 1
ORDER BY SubjectName;";

            using var con = new SqlConnection(GetConnectionString());
            con.Open();
            using var cmd = new SqlCommand(sql, con);
            using var r = cmd.ExecuteReader();

            var list = new List<SubjectListItemVM>();
            while (r.Read())
            {
                list.Add(new SubjectListItemVM
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1)
                });
            }
            return list;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Subject config from latest PricingRule
        // ─────────────────────────────────────────────────────────────────────────────
        public SubjectConfigVM? GetSubjectConfig(int subjectId)
        {
            const string sql = @"
SELECT TOP 1 s.SubjectID, s.SubjectName,
       pr.HourlyRate, pr.MinHours, pr.MaxHours, pr.MaxPointDiscount
FROM Subjects s
LEFT JOIN PricingRule pr ON pr.SubjectID = s.SubjectID
WHERE s.SubjectID = @sid AND s.IsActive = 1
ORDER BY pr.PricingRuleID DESC;";

            using var con = new SqlConnection(GetConnectionString());
            con.Open();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@sid", subjectId);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            if (r.IsDBNull(r.GetOrdinal("HourlyRate")))
                throw new InvalidOperationException("No pricing rule defined for this subject.");

            var hourly = r.GetDecimal(r.GetOrdinal("HourlyRate"));
            var minHours = r.GetDecimal(r.GetOrdinal("MinHours"));
            var maxHours = r.GetDecimal(r.GetOrdinal("MaxHours"));
            var maxPointDiscount = r.GetDecimal(r.GetOrdinal("MaxPointDiscount"));

            int minMin = (int)Math.Round(minHours * 60m, MidpointRounding.AwayFromZero);
            int maxMin = (int)Math.Round(maxHours * 60m, MidpointRounding.AwayFromZero);
            int maxPct = (int)Math.Floor(maxPointDiscount);

            return new SubjectConfigVM
            {
                SubjectId = r.GetInt32(r.GetOrdinal("SubjectID")),
                SubjectName = r.GetString(r.GetOrdinal("SubjectName")),
                HourlyRate = hourly,
                MinDurationMinutes = minMin,
                MaxDurationMinutes = maxMin,
                DurationStepMinutes = 30,
                MaxDiscountPercent = Math.Max(0, maxPct),
                DiscountStepPercent = 10
            };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Quote
        // ─────────────────────────────────────────────────────────────────────────────
        public QuoteVM CalculateQuote(int subjectId, int durationMinutes, int discountPercentRaw)
        {
            var cfg = GetSubjectConfig(subjectId) ?? throw new InvalidOperationException("Subject not found.");
            var duration = ClampToStep(durationMinutes, cfg.MinDurationMinutes, cfg.MaxDurationMinutes, cfg.DurationStepMinutes);
            var pct = ClampToStep(discountPercentRaw, 0, cfg.MaxDiscountPercent, cfg.DiscountStepPercent);

            var durationHours = (decimal)duration / 60m;
            var baseCost = cfg.HourlyRate * durationHours;

            var moneyDiscount = Math.Round(baseCost * pct / 100m, 2, MidpointRounding.AwayFromZero);
            var final = baseCost - moneyDiscount;

            return new QuoteVM
            {
                BaseCost = baseCost,
                DiscountPercentApplied = pct,
                PointsToCharge = pct, // 1 point per 1%
                MoneyDiscount = moneyDiscount,
                FinalCost = final
            };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Month availability (uses AdminAgendaService for slots, SQL for sessions)
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<AvailabilityMonthVM> GetAvailabilityMonthAsync(
            int subjectId, int durationMinutes, int year, int month, int? adminId)
        {
            var cfg = GetSubjectConfig(subjectId) ?? throw new InvalidOperationException("Subject not found.");
            var duration = ClampToStep(durationMinutes, cfg.MinDurationMinutes, cfg.MaxDurationMinutes, cfg.DurationStepMinutes);
            var durationTs = TimeSpan.FromMinutes(duration);

            var first = new DateTime(year, month, 1);
            var next = first.AddMonths(1);
            var cutoffDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2));

            // A) Availability blocks (re-use AdminAgendaService)
            var blocks = await _agenda.GetAvailabilityBlocksAsync(first, next, adminId);

            // B) Sessions that *block* time: Requested/Accepted/Paid
            const string sql = @"
SELECT SessionDate, StartTime, DurationHours
FROM TutoringSession
WHERE SubjectID = @sid
  AND (@aid IS NULL OR AdminID = @aid)
  AND SessionDate >= @from AND SessionDate < @to
  AND Status IN ('Requested','Accepted','Paid');";

            var sessions = new List<(DateOnly Date, TimeSpan Start, decimal DurHours)>();
            using (var con = await OpenAsync())
            using (var cmd = Cmd(con, sql, new Dictionary<string, object?>
            {
                ["@sid"] = subjectId,
                ["@aid"] = (object?)adminId ?? DBNull.Value,
                ["@from"] = first.Date,
                ["@to"] = next.Date
            }))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var date = DateOnly.FromDateTime(r.GetDateTime(0));
                    var start = (TimeSpan)r["StartTime"];
                    var dur = r.GetDecimal(2);
                    sessions.Add((date, start, dur));
                }
            }

            var days = new List<DayAvailabilityVM>();

            foreach (var grp in blocks
                     .Where(b => DateOnly.FromDateTime(b.BlockDate) >= cutoffDate)
                     .GroupBy(b => DateOnly.FromDateTime(b.BlockDate)))
            {
                var day = grp.Key;
                var slotVms = new List<SlotVM>();
                var daySessions = sessions.Where(s => s.Date == day).ToList();

                foreach (var b in grp.OrderBy(b => b.StartTime))
                {
                    var blockStart = b.StartTime;
                    var blockEnd = b.EndTime;

                    if (blockEnd - blockStart < durationTs) continue;

                    var options = new List<TimeOptionVM>();
                    for (var start = blockStart; start + durationTs <= blockEnd; start = start.Add(TimeSpan.FromMinutes(30)))
                    {
                        var end = start + durationTs;

                        bool clash = daySessions.Any(s =>
                        {
                            var sEnd = s.Start + TimeSpan.FromHours((double)s.DurHours);
                            return start < sEnd && end > s.Start;
                        });
                        if (clash) continue;

                        options.Add(new TimeOptionVM
                        {
                            SessionDate = day,
                            StartTime = start,
                            EndTime = end
                        });
                    }

                    if (options.Count == 0) continue;

                    slotVms.Add(new SlotVM
                    {
                        AvailabilityBlockId = b.AvailabilityBlockID,
                        BlockDate = day,
                        BlockStart = blockStart,
                        BlockEnd = blockEnd,
                        StartOptions = options
                    });
                }

                if (slotVms.Count > 0)
                {
                    days.Add(new DayAvailabilityVM { Day = day.Day, Slots = slotVms });
                }
            }

            return new AvailabilityMonthVM { Year = year, Month = month, Days = days };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Create booking request (validates range + conflicts in SQL)
        // ─────────────────────────────────────────────────────────────────────────────
        public BookingResult RequestBooking(int userId, BookingRequestVM dto, int? adminIdForSlotOwner = null)
        {
            var cfg = GetSubjectConfig(dto.SubjectId) ?? throw new InvalidOperationException("Subject not found.");

            // Parse "HH:mm"
            if (!TimeSpan.TryParseExact(dto.StartTime, "hh\\:mm", CultureInfo.InvariantCulture, out var startTs) &&
                !TimeSpan.TryParseExact(dto.StartTime, "h\\:mm", CultureInfo.InvariantCulture, out startTs) &&
                !TimeSpan.TryParse(dto.StartTime, out startTs))
            {
                return new BookingResult { Ok = false, Message = "Invalid start time format." };
            }

            var duration = ClampToStep(dto.DurationMinutes, cfg.MinDurationMinutes, cfg.MaxDurationMinutes, cfg.DurationStepMinutes);
            var pct = ClampToStep(dto.DiscountPercent, 0, cfg.MaxDiscountPercent, cfg.DiscountStepPercent);

            var durationTs = TimeSpan.FromMinutes(duration);
            var endTs = startTs + durationTs;

            // ≥ 2 days away
            var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
            if (dto.SessionDate < cutoff)
                return new BookingResult { Ok = false, Message = "Selected date must be at least 2 days in the future." };

            // 1) Find containing availability block
            const string findBlockSql = @"
SELECT TOP 1 AvailabilityBlockID, AdminID, BlockDate, StartTime, EndTime
FROM AvailabilityBlock
WHERE CAST(BlockDate AS date) = @date
  AND (@aid IS NULL OR AdminID = @aid)
  AND StartTime <= @start AND EndTime >= @end
ORDER BY StartTime;";

            int? blockAdminId = null;
            using (var con = OpenAsync().GetAwaiter().GetResult())
            using (var cmd = Cmd(con, findBlockSql, new Dictionary<string, object?>
            {
                ["@date"] = dto.SessionDate.ToDateTime(TimeOnly.MinValue).Date,
                ["@aid"] = (object?)adminIdForSlotOwner ?? DBNull.Value,
                ["@start"] = startTs,
                ["@end"] = endTs
            }))
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read())
                    return new BookingResult { Ok = false, Message = "Selected time is not within an availability block." };
                blockAdminId = r.GetInt32(r.GetOrdinal("AdminID"));
            }

            // 2) Check conflict in SQL
            const string conflictSql = @"
SELECT COUNT(*) 
FROM TutoringSession
WHERE SessionDate = @date
  AND SubjectID = @sid
  AND (@aid IS NULL OR AdminID = @aid)
  AND Status IN ('Requested','Accepted','Paid')
  AND StartTime < @end
  AND DATEADD(minute, CAST(DurationHours * 60 as int), StartTime) > @start;";

            int conflicts;
            using (var con = OpenAsync().GetAwaiter().GetResult())
            using (var cmd = Cmd(con, conflictSql, new Dictionary<string, object?>
            {
                ["@date"] = dto.SessionDate.ToDateTime(TimeOnly.MinValue).Date,
                ["@sid"] = dto.SubjectId,
                ["@aid"] = (object?)adminIdForSlotOwner ?? DBNull.Value,
                ["@start"] = startTs,
                ["@end"] = endTs
            }))
            {
                conflicts = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            if (conflicts > 0)
                return new BookingResult { Ok = false, Message = "That time is no longer available." };

            // 3) Compute price (authoritative)
            var quote = CalculateQuote(dto.SubjectId, duration, pct);
            var durationHours = (decimal)duration / 60m;

            // 4) Insert session
            const string insertSql = @"
INSERT INTO TutoringSession
(UserID, AdminID, SubjectID, SessionDate, StartTime, DurationHours, BaseCost, PointsSpent, Status)
OUTPUT INSERTED.TutoringSessionID
VALUES
(@userId, @adminId, @sid, @date, @start, @durHours, @base, @pts, @status);";

            int newId;
            using (var con = OpenAsync().GetAwaiter().GetResult())
            using (var cmd = Cmd(con, insertSql, new Dictionary<string, object?>
            {
                ["@userId"] = userId,
                ["@adminId"] = blockAdminId!,
                ["@sid"] = dto.SubjectId,
                ["@date"] = dto.SessionDate.ToDateTime(TimeOnly.MinValue).Date,
                ["@start"] = startTs,
                ["@durHours"] = durationHours,
                ["@base"] = quote.BaseCost,
                ["@pts"] = quote.PointsToCharge,
                ["@status"] = "Requested"
            }))
            {
                var scalar = cmd.ExecuteScalar();
                newId = scalar is null || scalar is DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            }

            return new BookingResult
            {
                Ok = newId > 0,
                BookingId = newId,
                Message = newId > 0 ? "Request sent to admin for approval." : "Could not create booking."
            };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────────
        private static int ClampToStep(int value, int min, int max, int step)
        {
            if (value < min) value = min;
            if (value > max) value = max;

            var offset = value - min;
            var snapped = (int)Math.Round(offset / (double)step) * step + min;

            if (snapped < min) snapped = min;
            if (snapped > max) snapped = max;
            return snapped;
        }
    }
}
