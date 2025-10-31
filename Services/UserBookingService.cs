#nullable enable
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
        private readonly PointsService _points;
        private const string ConnName = "AzureSql";

        public UserBookingService(IConfiguration config, AdminAgendaService agenda, PointsService points)
        {
            _config = config;
            _agenda = agenda;
            _points = points;
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

        private static SqlCommand Cmd(SqlConnection con, string sql, IDictionary<string, object?>? p = null, SqlTransaction? tx = null)
        {
            var cmd = new SqlCommand(sql, con, tx) { CommandType = CommandType.Text };
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

            // Points equal to the Rand discount (1 point = R1), rounded to nearest whole Rand
            var pointsToCharge = (int)Math.Round(moneyDiscount, MidpointRounding.AwayFromZero);

            var final = baseCost - moneyDiscount;

            return new QuoteVM
            {
                BaseCost = baseCost,
                DiscountPercentApplied = pct,
                PointsToCharge = pointsToCharge,   // Rands, not percent
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

            // B) Sessions that block time: Requested/Accepted/Paid — ACROSS ALL SUBJECTS
            const string sql = @"
SELECT SessionDate, StartTime, DurationHours, AdminID
FROM TutoringSession
WHERE SessionDate >= @from AND SessionDate < @to
  AND Status IN ('Requested','Accepted','Paid');";

            var sessions = new List<(DateOnly Date, TimeSpan Start, decimal DurHours, int AdminId)>();

            using (var con = await OpenAsync())
            using (var cmd = Cmd(con, sql, new Dictionary<string, object?>
            {
                ["@from"] = first.Date,
                ["@to"] = next.Date
            }))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var date = DateOnly.FromDateTime(r.GetDateTime(0));
                    var sessionStart = (TimeSpan)r["StartTime"];
                    var durHours = r.GetDecimal(2);
                    var adminIdForSession = r.GetInt32(3);
                    sessions.Add((date, sessionStart, durHours, adminIdForSession));
                }
            }

            var days = new List<DayAvailabilityVM>();

            foreach (var grp in blocks
                     .Where(b => DateOnly.FromDateTime(b.BlockDate) >= cutoffDate)
                     .GroupBy(b => DateOnly.FromDateTime(b.BlockDate)))
            {
                var day = grp.Key;
                var slotVms = new List<SlotVM>();

                foreach (var b in grp.OrderBy(b => b.StartTime))
                {
                    // Filter clashes to THIS block's admin
                    var daySessionsForAdmin = sessions
                        .Where(s => s.Date == day && s.AdminId == b.AdminID)
                        .ToList();

                    var blockStart = b.StartTime;
                    var blockEnd = b.EndTime;

                    if (blockEnd - blockStart < durationTs) continue;

                    var options = new List<TimeOptionVM>();

                    for (var candidateStart = blockStart;
                         candidateStart + durationTs <= blockEnd;
                         candidateStart = candidateStart.Add(TimeSpan.FromMinutes(30)))
                    {
                        var candidateEnd = candidateStart + durationTs;

                        bool clash = daySessionsForAdmin.Any(s =>
                        {
                            var sessionEnd = s.Start + TimeSpan.FromHours((double)s.DurHours);
                            // overlap if start < existing end AND end > existing start
                            return candidateStart < sessionEnd && candidateEnd > s.Start;
                        });
                        if (clash) continue;

                        options.Add(new TimeOptionVM
                        {
                            SessionDate = day,
                            StartTime = candidateStart,
                            EndTime = candidateEnd
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
        // Create booking request (validates range + conflicts) + points charge (atomic)
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<BookingResult> RequestBooking(int userId, BookingRequestVM dto, int? adminIdForSlotOwner = null)
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

            // Compute price (authoritative)
            var quote = CalculateQuote(dto.SubjectId, duration, pct);
            var durationHours = (decimal)duration / 60m;

            using var con = await OpenAsync();
            await using var tx = await con.BeginTransactionAsync();

            try
            {
                // 1) Find containing availability block (in tx scope)
                const string findBlockSql = @"
SELECT TOP 1 AvailabilityBlockID, AdminID, BlockDate, StartTime, EndTime
FROM AvailabilityBlock
WHERE CAST(BlockDate AS date) = @date
  AND (@aid IS NULL OR AdminID = @aid)
  AND StartTime <= @start AND EndTime >= @end
ORDER BY StartTime;";

                int? blockAdminId = null;
                using (var cmd = Cmd(con, findBlockSql, new Dictionary<string, object?>
                {
                    ["@date"] = dto.SessionDate.ToDateTime(TimeOnly.MinValue).Date,
                    ["@aid"] = (object?)adminIdForSlotOwner ?? DBNull.Value,
                    ["@start"] = startTs,
                    ["@end"] = endTs
                }, (SqlTransaction)tx))
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    if (!await r.ReadAsync())
                    {
                        await tx.RollbackAsync();
                        return new BookingResult { Ok = false, Message = "Selected time is not within an availability block." };
                    }
                    blockAdminId = r.GetInt32(r.GetOrdinal("AdminID"));
                }

                // 2) Check conflict in SQL (left as-is per your preference)
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
                using (var cmd = Cmd(con, conflictSql, new Dictionary<string, object?>
                {
                    ["@date"] = dto.SessionDate.ToDateTime(TimeOnly.MinValue).Date,
                    ["@sid"] = dto.SubjectId,
                    ["@aid"] = (object?)adminIdForSlotOwner ?? DBNull.Value,
                    ["@start"] = startTs,
                    ["@end"] = endTs
                }, (SqlTransaction)tx))
                {
                    conflicts = Convert.ToInt32(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                }
                if (conflicts > 0)
                {
                    await tx.RollbackAsync();
                    return new BookingResult { Ok = false, Message = "That time is no longer available." };
                }

                // 3) Gate by CURRENT points balance — computed inside the SAME tx/connection
                var currentPoints = await _points.GetCurrentAsync(userId, con, (SqlTransaction)tx);
                if (currentPoints < quote.PointsToCharge)
                {
                    await tx.RollbackAsync();
                    return new BookingResult { Ok = false, Message = "Insufficient points for this booking's discount." };
                }

                // 4) Insert session
                const string insertSql = @"
INSERT INTO TutoringSession
(UserID, AdminID, SubjectID, SessionDate, StartTime, DurationHours, BaseCost, PointsSpent, Status)
OUTPUT INSERTED.TutoringSessionID
VALUES
(@userId, @adminId, @sid, @date, @start, @durHours, @base, @pts, @status);";

                int newSessionId;
                using (var cmd = new SqlCommand(insertSql, con, (SqlTransaction)tx))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@adminId", blockAdminId!);
                    cmd.Parameters.AddWithValue("@sid", dto.SubjectId);
                    cmd.Parameters.AddWithValue("@date", dto.SessionDate.ToDateTime(TimeOnly.MinValue).Date);
                    cmd.Parameters.AddWithValue("@start", startTs);
                    cmd.Parameters.AddWithValue("@durHours", durationHours);
                    cmd.Parameters.AddWithValue("@base", quote.BaseCost);
                    cmd.Parameters.AddWithValue("@pts", quote.PointsToCharge); // may be 0
                    cmd.Parameters.AddWithValue("@status", "Requested");

                    var scalar = await cmd.ExecuteScalarAsync();
                    newSessionId = (scalar is null || scalar is DBNull)
                        ? 0
                        : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
                    if (newSessionId <= 0)
                    {
                        await tx.RollbackAsync();
                        return new BookingResult { Ok = false, Message = "Could not create booking." };
                    }
                }

                // 5) Create Spent receipt only if points > 0
                if (quote.PointsToCharge > 0)
                {
                    var spentReceiptId = await _points.CreateSpentForSessionIdempotentAsync(
                        userId,
                        blockAdminId!.Value,
                        newSessionId,
                        quote.PointsToCharge,
                        con,
                        (SqlTransaction)tx);

                    if (spentReceiptId is null)
                    {
                        await tx.RollbackAsync();
                        return new BookingResult { Ok = false, Message = "Could not charge points for booking." };
                    }
                }

                // 6) Commit everything
                await tx.CommitAsync();

                return new BookingResult
                {
                    Ok = true,
                    BookingId = newSessionId,
                    Message = "Request sent to admin for approval."
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new BookingResult { Ok = false, Message = $"Booking failed: {ex.Message}" };
            }
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
