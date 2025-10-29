#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace J_Tutors_Web_Platform.Services
{
    // DTOs
    public sealed record ActiveSubjectDto(
        int SubjectID,
        string SubjectName,
        decimal HourlyRate,
        decimal MinHours,
        decimal MaxHours,
        decimal MaxPointDiscount);

    public sealed record QuoteResult(
        decimal HoursPerSession,
        int SessionCount,
        decimal HourlyRate,
        decimal BaseTotal,
        decimal PointsPercentApplied,
        decimal PointsValueZar,
        decimal PayableAfterPoints);

    public sealed record AvailabilityCapacity(
        int AdminID,
        DateTime Date,
        TimeSpan Start,
        TimeSpan End,
        int BlockMinutes,
        int MinutesPerSession,
        int SlotCount);

    public sealed record SlotOption(DateTime Date, TimeSpan StartTime, int Minutes);

    /// <summary>
    /// SERVICE: UserBookingService
    /// PURPOSE: Subjects & pricing read; quoting; availability; reservation.
    /// STORAGE: Azure SQL via ADO.NET (SqlConnection/SqlCommand).
    /// </summary>
    public sealed class UserBookingService
    {
        private readonly string _connStr;
        private readonly ILogger<UserBookingService> _log;

        public UserBookingService(IConfiguration cfg, ILogger<UserBookingService> log)
        {
            _connStr = cfg.GetConnectionString("AzureSql")
                ?? throw new ArgumentException("Missing DB connection string.", nameof(cfg));
            _log = log;
        }

        // -------------------- Subjects & Pricing --------------------

        public async Task<IReadOnlyList<ActiveSubjectDto>> GetActiveSubjectsAsync()
        {
            const string sql = @"
SELECT s.SubjectID, s.SubjectName,
       ISNULL(pr.HourlyRate,0)       AS HourlyRate,
       ISNULL(pr.MinHours,0)         AS MinHours,
       ISNULL(pr.MaxHours,0)         AS MaxHours,
       ISNULL(pr.MaxPointDiscount,0) AS MaxPointDiscount
FROM   dbo.Subjects s
LEFT   JOIN dbo.PricingRule pr ON pr.SubjectID = s.SubjectID
WHERE  s.IsActive = 1
ORDER  BY s.SubjectName;";

            var list = new List<ActiveSubjectDto>();

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new ActiveSubjectDto(
                    r.GetInt32(r.GetOrdinal("SubjectID")),
                    r.GetString(r.GetOrdinal("SubjectName")),
                    r.IsDBNull(r.GetOrdinal("HourlyRate")) ? 0m : Convert.ToDecimal(r["HourlyRate"], CultureInfo.InvariantCulture),
                    r.IsDBNull(r.GetOrdinal("MinHours")) ? 0m : Convert.ToDecimal(r["MinHours"], CultureInfo.InvariantCulture),
                    r.IsDBNull(r.GetOrdinal("MaxHours")) ? 0m : Convert.ToDecimal(r["MaxHours"], CultureInfo.InvariantCulture),
                    r.IsDBNull(r.GetOrdinal("MaxPointDiscount")) ? 0m : Convert.ToDecimal(r["MaxPointDiscount"], CultureInfo.InvariantCulture)
                ));
            }

            return list;
        }

        /// <summary>
        /// Server-authoritative quote. Locks hours to 0.5 increments and points to 10% increments capped by subject rule.
        /// </summary>
        public QuoteResult Quote(
            decimal hoursPerSession,
            int sessionCount,
            decimal hourlyRate,
            decimal requestedPointsPercent,
            decimal capPercent)
        {
            // 0.5 hour step (round away from zero)
            hoursPerSession = Math.Round(hoursPerSession * 2, MidpointRounding.AwayFromZero) / 2m;
            if (hoursPerSession <= 0) throw new ArgumentException("Hours per session must be > 0.", nameof(hoursPerSession));
            if (sessionCount <= 0) throw new ArgumentException("Session count must be > 0.", nameof(sessionCount));
            if (hourlyRate < 0) throw new ArgumentException("Hourly rate must be ≥ 0.", nameof(hourlyRate));

            // Cap + lock to 10%
            var pct = Math.Clamp(requestedPointsPercent, 0m, capPercent);
            pct = Math.Round(pct / 10m, MidpointRounding.AwayFromZero) * 10m;

            var baseTotal = Math.Round(sessionCount * hoursPerSession * hourlyRate, 2);
            var pointsValue = Math.Round(baseTotal * (pct / 100m), 2);
            var payable = Math.Max(0, baseTotal - pointsValue);

            return new QuoteResult(hoursPerSession, sessionCount, hourlyRate, baseTotal, pct, pointsValue, payable);
        }

        // -------------------- Availability → SlotOptions --------------------

        public async Task<IReadOnlyList<SlotOption>> GetAvailableSlotsAsync(
            int adminId,
            decimal hoursPerSession,
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            var minutesPerSession = (int)Math.Round(hoursPerSession * 60m);
            if (minutesPerSession <= 0) return Array.Empty<SlotOption>();

            const string sqlBlocks = @"
SELECT AvailabilityBlockID, BlockDate, StartTime, EndTime
FROM   dbo.AvailabilityBlock
WHERE  AdminID = @a
  AND  BlockDate >= @fromDate
  AND  BlockDate <  @toDate
ORDER  BY BlockDate, StartTime;";

            var slots = new List<SlotOption>();

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using (var cmd = new SqlCommand(sqlBlocks, conn))
            {
                cmd.Parameters.AddWithValue("@a", adminId);
                cmd.Parameters.AddWithValue("@fromDate", fromInclusive.Date);
                cmd.Parameters.AddWithValue("@toDate", toExclusive.Date);

                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var date = ((DateTime)r["BlockDate"]).Date;
                    var st = (TimeSpan)r["StartTime"];
                    var et = (TimeSpan)r["EndTime"];

                    // Slide across the block in 15-minute increments
                    var cursor = st;
                    while (cursor.Add(TimeSpan.FromMinutes(minutesPerSession)) <= et)
                    {
                        if (await IsFreeAsync(conn, adminId, date, cursor, minutesPerSession))
                            slots.Add(new SlotOption(date, cursor, minutesPerSession));

                        cursor = cursor.Add(TimeSpan.FromMinutes(15));
                    }
                }
            }

            // Keep results reasonable
            return slots.Count > 2000 ? slots.Take(2000).ToList() : slots;
        }

        // Overlap check using SessionDate (date) + StartTime (time)
        private static async Task<bool> IsFreeAsync(
            SqlConnection conn, int adminId, DateTime date, TimeSpan startTime, int minutes)
        {
            const string sql = @"
DECLARE @startDt  datetime2 = DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', @startTime), CAST(@date AS datetime2));
DECLARE @endDt    datetime2 = DATEADD(MINUTE, @mins, @startDt);

-- overlap if existingStart < newEnd AND existingEnd > newStart
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM   dbo.TutoringSession s
    WHERE  s.AdminID = @a
      AND  s.Status IN (0,1,2) -- Pending/Accepted/Scheduled (align with your enum)
      AND  DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', s.StartTime), CAST(s.SessionDate AS datetime2)) < @endDt
      AND  DATEADD(MINUTE, CAST(s.DurationHours * 60 AS int),
           DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', s.StartTime), CAST(s.SessionDate AS datetime2))) > @startDt
) THEN 0 ELSE 1 END;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@a", adminId);
            cmd.Parameters.AddWithValue("@date", date.Date);
            cmd.Parameters.AddWithValue("@startTime", startTime);
            cmd.Parameters.AddWithValue("@mins", minutes);

            var ok = (int)await cmd.ExecuteScalarAsync();
            return ok == 1;
        }

        // -------------------- Reserve (create pending) & consume availability --------------------

        public async Task<int> CreatePendingSessionsAsync(
            int userId,
            int adminId,
            int subjectId,
            decimal hoursPerSession,
            decimal hourlyRate,
            decimal pointsPercentApplied,
            IEnumerable<(DateTime date, TimeSpan startTime)> selected)
        {
            var minutes = (int)Math.Round(hoursPerSession * 60m);
            var created = 0;

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                foreach (var (date, start) in selected.OrderBy(s => s.date).ThenBy(s => s.startTime))
                {
                    if (!await IsFreeAsync(conn, adminId, date, start, minutes))
                        continue;

                    var baseCost = Math.Round(hoursPerSession * hourlyRate, 2);
                    var discount = Math.Round(baseCost * (pointsPercentApplied / 100m), 2);
                    var pointsUsed = (int)Math.Round(discount, MidpointRounding.AwayFromZero); // 1:1

                    const string ins = @"
INSERT INTO dbo.TutoringSession
    (UserID, AdminID, SubjectID, SessionDate, StartTime, DurationHours, BaseCost, PointsSpent, Status)
VALUES
    (@u, @a, @s, @d, @t, @dur, @cost, @pts, 0); -- 0 = Pending";

                    await using (var cmd = new SqlCommand(ins, conn, (SqlTransaction)tx))
                    {
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.Parameters.AddWithValue("@a", adminId);
                        cmd.Parameters.AddWithValue("@s", subjectId);
                        cmd.Parameters.AddWithValue("@d", date.Date);
                        cmd.Parameters.AddWithValue("@t", start);
                        cmd.Parameters.AddWithValue("@dur", hoursPerSession);
                        cmd.Parameters.AddWithValue("@cost", baseCost);
                        cmd.Parameters.AddWithValue("@pts", pointsUsed);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    await ConsumeAvailabilityAsync(conn, (SqlTransaction)tx, adminId, date, start, minutes);
                    created++;
                }

                await tx.CommitAsync();
                return created;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to create pending sessions.");
                await tx.RollbackAsync();
                throw;
            }
        }

        private static async Task ConsumeAvailabilityAsync(
            SqlConnection conn, SqlTransaction tx, int adminId, DateTime date, TimeSpan start, int minutes)
        {
            var end = start.Add(TimeSpan.FromMinutes(minutes));

            const string find = @"
SELECT TOP 1 AvailabilityBlockID, StartTime, EndTime
FROM   dbo.AvailabilityBlock
WHERE  AdminID = @a
  AND  BlockDate = @d
  AND  @start >= StartTime
  AND  @end   <= EndTime
ORDER  BY StartTime;";

            int? id = null;
            TimeSpan? oldStart = null;
            TimeSpan? oldEnd = null;

            await using (var cmd = new SqlCommand(find, conn, tx))
            {
                cmd.Parameters.AddWithValue("@a", adminId);
                cmd.Parameters.AddWithValue("@d", date.Date);
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);

                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    id = r.GetInt32(0);
                    oldStart = (TimeSpan)r["StartTime"];
                    oldEnd = (TimeSpan)r["EndTime"];
                }
            }

            if (id is null) return;

            // consume head/tail/split
            if (start <= oldStart && end >= oldEnd)
            {
                await using var del = new SqlCommand("DELETE FROM dbo.AvailabilityBlock WHERE AvailabilityBlockID=@id", conn, tx);
                del.Parameters.AddWithValue("@id", id.Value);
                await del.ExecuteNonQueryAsync();
            }
            else if (start <= oldStart && end < oldEnd)
            {
                await using var upd = new SqlCommand("UPDATE dbo.AvailabilityBlock SET StartTime=@st WHERE AvailabilityBlockID=@id", conn, tx);
                upd.Parameters.AddWithValue("@st", end);
                upd.Parameters.AddWithValue("@id", id.Value);
                await upd.ExecuteNonQueryAsync();
            }
            else if (start > oldStart && end >= oldEnd)
            {
                await using var upd = new SqlCommand("UPDATE dbo.AvailabilityBlock SET EndTime=@et WHERE AvailabilityBlockID=@id", conn, tx);
                upd.Parameters.AddWithValue("@et", start);
                upd.Parameters.AddWithValue("@id", id.Value);
                await upd.ExecuteNonQueryAsync();
            }
            else
            {
                const string split = @"
UPDATE dbo.AvailabilityBlock SET EndTime=@leftEnd WHERE AvailabilityBlockID=@id;
INSERT INTO dbo.AvailabilityBlock (AdminID, BlockDate, StartTime, EndTime)
VALUES (@a, @d, @rightStart, @rightEnd);";
                await using var cmd = new SqlCommand(split, conn, tx);
                cmd.Parameters.AddWithValue("@leftEnd", start);
                cmd.Parameters.AddWithValue("@id", id.Value);
                cmd.Parameters.AddWithValue("@a", adminId);
                cmd.Parameters.AddWithValue("@d", date.Date);
                cmd.Parameters.AddWithValue("@rightStart", end);
                cmd.Parameters.AddWithValue("@rightEnd", oldEnd!);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<int?> FindAdminWithAvailabilityAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            int? preferredAdminId = null)
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            Console.WriteLine($"[DEBUG] FindAdminWithAvailability window = [{fromInclusive:yyyy-MM-dd} .. {toExclusive:yyyy-MM-dd})");

            // Count rows in window (date-only compare)
            const string SQL_COUNT = @"
SELECT COUNT(*)
FROM dbo.AvailabilityBlock
WHERE CAST(BlockDate AS date) >= @from
  AND CAST(BlockDate AS date) <  @to;";

            int inRangeCount;
            await using (var countCmd = new SqlCommand(SQL_COUNT, conn))
            {
                countCmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Date;
                countCmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Date;
                inRangeCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }
            Console.WriteLine($"[DEBUG] AvailabilityBlock rows in range: {inRangeCount}");

            // Preferred admin first (only if present in window)
            if (preferredAdminId is int pid)
            {
                const string SQL_PREF = @"
SELECT TOP 1 1
FROM dbo.AvailabilityBlock
WHERE AdminID = @a
  AND CAST(BlockDate AS date) >= @from
  AND CAST(BlockDate AS date) <  @to;";
                await using var prefCmd = new SqlCommand(SQL_PREF, conn);
                prefCmd.Parameters.Add("@a", SqlDbType.Int).Value = pid;
                prefCmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Date;
                prefCmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Date;

                var has = await prefCmd.ExecuteScalarAsync();
                Console.WriteLine($"[DEBUG] Preferred AdminID={pid} has rows in window? {(has != null ? "YES" : "NO")}");
                if (has != null) return pid;
            }

            // Any admin with rows in window
            const string SQL_PICK_IN_RANGE = @"
SELECT TOP 1 AdminID
FROM dbo.AvailabilityBlock
WHERE CAST(BlockDate AS date) >= @from
  AND CAST(BlockDate AS date) <  @to
ORDER BY AdminID;";
            await using (var pickCmd = new SqlCommand(SQL_PICK_IN_RANGE, conn))
            {
                pickCmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Date;
                pickCmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Date;
                var val = await pickCmd.ExecuteScalarAsync();
                if (val != null && val != DBNull.Value)
                {
                    var got = Convert.ToInt32(val);
                    Console.WriteLine($"[DEBUG] Picked AdminID in window: {got}");
                    return got;
                }
            }

            // Diagnostics if nothing matched the window
            const string SQL_SPAN = @"SELECT MIN(CAST(BlockDate AS date)), MAX(CAST(BlockDate AS date)) FROM dbo.AvailabilityBlock;";
            await using (var spanCmd = new SqlCommand(SQL_SPAN, conn))
            await using (var r = await spanCmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    var min = r.IsDBNull(0) ? (DateTime?)null : r.GetDateTime(0);
                    var max = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1);
                    Console.WriteLine($"[DEBUG] AvailabilityBlock global span: min={min:yyyy-MM-dd}, max={max:yyyy-MM-dd}");
                }
            }

            const string SQL_ADMINS = @"SELECT DISTINCT AdminID FROM dbo.AvailabilityBlock ORDER BY AdminID;";
            await using (var adminsCmd = new SqlCommand(SQL_ADMINS, conn))
            await using (var r2 = await adminsCmd.ExecuteReaderAsync())
            {
                var ids = new List<int>();
                while (await r2.ReadAsync()) ids.Add(r2.GetInt32(0));
                Console.WriteLine($"[DEBUG] Distinct AdminIDs present: {(ids.Count == 0 ? "(none)" : string.Join(", ", ids))}");
            }

            // Fallback: return any AdminID at all so UI can still progress
            const string SQL_ANY = @"SELECT TOP 1 AdminID FROM dbo.AvailabilityBlock ORDER BY AdminID;";
            await using (var anyCmd = new SqlCommand(SQL_ANY, conn))
            {
                var any = await anyCmd.ExecuteScalarAsync();
                if (any != null && any != DBNull.Value)
                {
                    var got = Convert.ToInt32(any);
                    Console.WriteLine($"[DEBUG] Fallback AdminID (outside window): {got}");
                    return got;
                }
            }

            Console.WriteLine("[DEBUG] No AvailabilityBlock rows exist.");
            return null;
        }

        /// <summary>
        /// Capacity rows (internal; reused by debug slot counting).
        /// </summary>
        public async Task<IReadOnlyList<AvailabilityCapacity>> GetCapacityReportAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            int minutesPerSession,
            int? adminId = null)
        {
            if (minutesPerSession <= 0) return Array.Empty<AvailabilityCapacity>();

            const string SQL = @"
SELECT AdminID, BlockDate, StartTime, EndTime
FROM dbo.AvailabilityBlock
WHERE CAST(BlockDate AS date) >= @from
  AND CAST(BlockDate AS date) <  @to
  /**adminFilter**/
ORDER BY BlockDate, StartTime;";

            var rows = new List<AvailabilityCapacity>();

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var sql = SQL.Replace("/**adminFilter**/", adminId.HasValue ? "AND AdminID = @a" : string.Empty);

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Date;
            cmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Date;
            if (adminId.HasValue) cmd.Parameters.Add("@a", SqlDbType.Int).Value = adminId.Value;

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var a = r.GetInt32(r.GetOrdinal("AdminID"));
                var d = ((DateTime)r["BlockDate"]).Date;
                var st = (TimeSpan)r["StartTime"];
                var et = (TimeSpan)r["EndTime"];

                var blockMinutes = (int)(et - st).TotalMinutes;

                int slotCount = 0;
                if (blockMinutes >= minutesPerSession)
                {
                    // number of (minutesPerSession)-long windows if we slide in 15-minute steps
                    slotCount = ((blockMinutes - minutesPerSession) / 15) + 1;
                    if (slotCount < 0) slotCount = 0;
                }

                rows.Add(new AvailabilityCapacity(
                    AdminID: a,
                    Date: d,
                    Start: st,
                    End: et,
                    BlockMinutes: blockMinutes,
                    MinutesPerSession: minutesPerSession,
                    SlotCount: slotCount));
            }

            return rows
                .OrderByDescending(x => x.SlotCount)
                .ThenBy(x => x.Date)
                .ThenBy(x => x.AdminID)
                .ToList();
        }

        // ========================== DEBUG: slot counts by length ==========================
        // TEMP/DEBUG: For a list of session lengths (in minutes), compute the total number
        // of sliding-window slots available in the next N days across all admins (or one).
        // Uses the same 15-minute step rule as GetCapacityReportAsync.
        public async Task<IDictionary<int, int>> GetGlobalSlotCountsAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            IEnumerable<int> minutesPerSessionList,
            int? adminId = null)
        {
            var result = new Dictionary<int, int>();

            // Deduplicate + sort ascending for predictable output
            var lengths = minutesPerSessionList
                .Where(m => m > 0)
                .Distinct()
                .OrderBy(m => m)
                .ToArray();

            foreach (var m in lengths)
            {
                var rows = await GetCapacityReportAsync(fromInclusive, toExclusive, m, adminId);
                var total = rows.Sum(r => r.SlotCount);
                result[m] = total;
            }

            return result;
        }
        // ======================== END DEBUG: slot counts by length ========================
    }
}
