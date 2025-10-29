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
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;

namespace J_Tutors_Web_Platform.Services
{
    /// <summary>
    /// SERVICE: AdminAgendaService
    /// PURPOSE: Backend for Admin "Agenda" (Slots | Inbox | Calendar).
    /// STORAGE: Azure SQL via ADO.NET.
    /// 
    /// Conventions:
    /// - Dates in DB:
    ///     AvailabilityBlock.BlockDate: date or datetime (we treat date portion).
    ///     TutoringSession.SessionDate: date (mapped to C# DateOnly here).
    /// - Times in DB:
    ///     AvailabilityBlock.StartTime / EndTime: time
    ///     TutoringSession.StartTime: time
    /// - Status:
    ///     TutoringSession.Status: int (mapped to TutoringSessionStatus)
    /// </summary>
    public sealed class AdminAgendaService
    {
        private readonly string _connStr;
        private readonly ILogger<AdminAgendaService> _log;

        public AdminAgendaService(IConfiguration cfg, ILogger<AdminAgendaService> log)
        {
            _connStr = cfg.GetConnectionString("AzureSql")
                ?? throw new ArgumentException("Missing DB connection string 'AzureSql'.");
            _log = log;
        }

        // ============================================================================
        // SECTION: Utility helpers
        // ============================================================================

        private static (DateTime from, DateTime to) MonthWindow(int year, int month)
        {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1);
            return (from, to);
        }

        private static DateTime DateOnlyToDateTime(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);
        private static DateOnly DateTimeToDateOnly(DateTime d) => DateOnly.FromDateTime(d);

        private static TutoringSession MapSession(SqlDataReader r)
        {
            // NOTE: SessionDate is "date" in SQL; use DateOnly
            var date = (DateTime)r["SessionDate"];
            return new TutoringSession
            {
                TutoringSessionID = Convert.ToInt32(r["TutoringSessionID"]),
                UserID = Convert.ToInt32(r["UserID"]),
                AdminID = Convert.ToInt32(r["AdminID"]),
                SubjectID = Convert.ToInt32(r["SubjectID"]),
                SessionDate = DateOnly.FromDateTime(date),
                StartTime = (TimeSpan)r["StartTime"],
                DurationHours = Convert.ToDecimal(r["DurationHours"], CultureInfo.InvariantCulture),
                BaseCost = Convert.ToDecimal(r["BaseCost"], CultureInfo.InvariantCulture),
                PointsSpent = Convert.ToInt32(r["PointsSpent"]),
                Status = (TutoringSessionStatus)Convert.ToInt32(r["Status"]),
                CancellationDate = r["CancellationDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["CancellationDate"]),
                PaidDate = r["PaidDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["PaidDate"])
            };
        }

        private static AvailabilityBlock MapBlock(SqlDataReader r)
        {
            return new AvailabilityBlock
            {
                AvailabilityBlockID = Convert.ToInt32(r["AvailabilityBlockID"]),
                AdminID = Convert.ToInt32(r["AdminID"]),
                BlockDate = Convert.ToDateTime(r["BlockDate"]).Date,
                StartTime = (TimeSpan)r["StartTime"],
                EndTime = (TimeSpan)r["EndTime"]
            };
        }

        // ============================================================================
        // SECTION: Agenda Shell counts
        // ============================================================================

        /// <summary>
        /// Returns counts per high-level status for the Agenda header badges.
        /// </summary>
        public async Task<(int scheduled, int accepted, int paid, int cancelled)> GetAgendaCountsAsync()
        {
            const string SQL = @"SELECT Status FROM dbo.TutoringSession;";

            int scheduled = 0, accepted = 0, paid = 0, cancelled = 0;

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(SQL, conn);
            await conn.OpenAsync();

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var statusObj = r["Status"];

                // ---- robust parse: supports int, numeric string, or enum name string ----
                TutoringSessionStatus status;
                try
                {
                    if (statusObj is int i) status = (TutoringSessionStatus)i;
                    else
                    {
                        var s = Convert.ToString(statusObj, CultureInfo.InvariantCulture)?.Trim();

                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asInt))
                            status = (TutoringSessionStatus)asInt;
                        else if (Enum.TryParse<TutoringSessionStatus>(s, true, out var asEnum))
                            status = asEnum;
                        else
                            status = TutoringSessionStatus.Scheduled; // safe default
                    }
                }
                catch
                {
                    status = TutoringSessionStatus.Scheduled;
                }

                switch (status)
                {
                    case TutoringSessionStatus.Scheduled: scheduled++; break;
                    case TutoringSessionStatus.Accepted: accepted++; break;
                    case TutoringSessionStatus.Paid: paid++; break;
                    case TutoringSessionStatus.Cancelled: cancelled++; break;
                    default: break; // ignore Denied/Completed for these counts
                }
            }

            return (scheduled, accepted, paid, cancelled);
        }


        // ============================================================================
        // SECTION: Slots tab (Availability blocks)
        // ============================================================================

        /// <summary>
        /// List availability blocks (optionally filtered by window and/or admin).
        /// </summary>
        public async Task<IReadOnlyList<AvailabilityBlock>> GetAvailabilityBlocksAsync(
            DateTime? fromInclusive = null,
            DateTime? toExclusive = null,
            int? adminId = null)
        {
            // Base
            var sql = @"
SELECT AvailabilityBlockID, AdminID, BlockDate, StartTime, EndTime
FROM dbo.AvailabilityBlock
WHERE 1=1";

            if (fromInclusive.HasValue) sql += " AND CAST(BlockDate AS date) >= @from";
            if (toExclusive.HasValue) sql += " AND CAST(BlockDate AS date) <  @to";
            if (adminId.HasValue) sql += " AND AdminID = @a";
            sql += " ORDER BY BlockDate, StartTime";

            var list = new List<AvailabilityBlock>();

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            if (fromInclusive.HasValue) cmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Value.Date;
            if (toExclusive.HasValue) cmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Value.Date;
            if (adminId.HasValue) cmd.Parameters.Add("@a", SqlDbType.Int).Value = adminId.Value;

            await conn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(MapBlock(r));

            return list;
        }

        /// <summary>
        /// Creates an availability block (adminId + date + start + duration minutes).
        /// </summary>
        public async Task<int> CreateAvailabilityBlockAsync(int adminId, DateTime date, TimeSpan start, int durationMinutes)
        {
            if (durationMinutes <= 0) throw new ArgumentException("Duration must be positive.");
            var end = start.Add(TimeSpan.FromMinutes(durationMinutes));

            const string sql = @"
INSERT INTO dbo.AvailabilityBlock (AdminID, BlockDate, StartTime, EndTime)
VALUES (@a, @d, @st, @et);
SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@a", SqlDbType.Int).Value = adminId;
            cmd.Parameters.Add("@d", SqlDbType.DateTime2).Value = date.Date;
            cmd.Parameters.Add("@st", SqlDbType.Time).Value = start;
            cmd.Parameters.Add("@et", SqlDbType.Time).Value = end;

            await conn.OpenAsync();
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _log.LogInformation("Created AvailabilityBlock #{Id} (Admin={Admin} {Date:yyyy-MM-dd} {Start}-{End})",
                id, adminId, date.Date, start, end);
            return id;
        }

        /// <summary>
        /// Deletes an availability block by id.
        /// </summary>
        public async Task<int> DeleteAvailabilityBlockAsync(int availabilityBlockId)
        {
            const string sql = @"DELETE FROM dbo.AvailabilityBlock WHERE AvailabilityBlockID=@id;";
            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = availabilityBlockId;
            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            _log.LogInformation("Deleted AvailabilityBlock #{Id} -> rows {Rows}", availabilityBlockId, rows);
            return rows;
        }

        // ============================================================================
        // SECTION: Inbox tab (bucket by status)
        // ============================================================================

        /// <summary>
        /// Returns sessions bucketed by status for the Inbox tab.
        /// </summary>
        public async Task<(List<TutoringSession> scheduled,
                           List<TutoringSession> accepted,
                           List<TutoringSession> paid,
                           List<TutoringSession> cancelled)> GetInboxBucketsAsync()
        {
            var all = await GetSessionsAsync(null, null, null);
            return (
                all.Where(s => s.Status == TutoringSessionStatus.Scheduled).ToList(),
                all.Where(s => s.Status == TutoringSessionStatus.Accepted).ToList(),
                all.Where(s => s.Status == TutoringSessionStatus.Paid).ToList(),
                all.Where(s => s.Status == TutoringSessionStatus.Cancelled).ToList()
            );
        }

        /// <summary>
        /// Generic fetch for sessions (optional window & admin filtering).
        /// </summary>
        public async Task<List<TutoringSession>> GetSessionsAsync(
            DateTime? fromInclusive,
            DateTime? toExclusive,
            int? adminId)
        {
            var sql = @"
SELECT TutoringSessionID, UserID, AdminID, SubjectID, SessionDate,
       StartTime, DurationHours, BaseCost, PointsSpent,
       Status, CancellationDate, PaidDate
FROM dbo.TutoringSession
WHERE 1=1";

            if (fromInclusive.HasValue) sql += " AND SessionDate >= @from";
            if (toExclusive.HasValue) sql += " AND SessionDate <  @to";
            if (adminId.HasValue) sql += " AND AdminID = @a";
            sql += " ORDER BY SessionDate, StartTime";

            var list = new List<TutoringSession>();

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            if (fromInclusive.HasValue) cmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Value.Date;
            if (toExclusive.HasValue) cmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Value.Date;
            if (adminId.HasValue) cmd.Parameters.Add("@a", SqlDbType.Int).Value = adminId.Value;

            await conn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(MapSession(r));

            return list;
        }

        // ============================================================================
        // SECTION: Calendar tab
        // ============================================================================

        /// <summary>
        /// Sessions for month-grid calendar. Always includes Accepted;
        /// optionally includes Scheduled.
        /// </summary>
        public async Task<List<TutoringSession>> GetSessionsForCalendarAsync(
            int year, int month, bool includeScheduled, int? adminId = null)
        {
            var (from, to) = MonthWindow(year, month);
            var all = await GetSessionsAsync(from, to, adminId);

            return all.Where(s =>
                s.Status == TutoringSessionStatus.Accepted ||
                (includeScheduled && s.Status == TutoringSessionStatus.Scheduled)
            ).ToList();
        }

        // ============================================================================
        // SECTION: Session actions (Accept / Deny / Cancel / MarkPaid)
        // ============================================================================

        /// <summary>
        /// Accept a Scheduled session -> Accepted.
        /// </summary>
        public async Task<int> AcceptSessionAsync(int sessionId)
        {
            const string sql = @"
UPDATE dbo.TutoringSession
SET Status = @accepted
WHERE TutoringSessionID = @id AND Status = @scheduled;";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@accepted", SqlDbType.Int).Value = (int)TutoringSessionStatus.Accepted;
            cmd.Parameters.Add("@scheduled", SqlDbType.Int).Value = (int)TutoringSessionStatus.Scheduled;
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = sessionId;

            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            _log.LogInformation("AcceptSession #{Id} -> rows {Rows}", sessionId, rows);
            return rows;
        }

        /// <summary>
        /// Deny a Scheduled session -> Denied.
        /// </summary>
        public async Task<int> DenySessionAsync(int sessionId)
        {
            const string sql = @"
UPDATE dbo.TutoringSession
SET Status = @denied
WHERE TutoringSessionID = @id AND Status = @scheduled;";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@denied", SqlDbType.Int).Value = (int)TutoringSessionStatus.Denied;
            cmd.Parameters.Add("@scheduled", SqlDbType.Int).Value = (int)TutoringSessionStatus.Scheduled;
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = sessionId;

            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            _log.LogInformation("DenySession #{Id} -> rows {Rows}", sessionId, rows);
            return rows;
        }

        /// <summary>
        /// Cancel a not-yet-completed session -> Cancelled (sets CancellationDate=UTC now).
        /// Allowed from Scheduled or Accepted.
        /// </summary>
        public async Task<int> CancelSessionAsync(int sessionId)
        {
            const string sql = @"
UPDATE dbo.TutoringSession
SET Status = @cancelled,
    CancellationDate = SYSUTCDATETIME()
WHERE TutoringSessionID = @id
  AND Status IN (@scheduled, @accepted);";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@cancelled", SqlDbType.Int).Value = (int)TutoringSessionStatus.Cancelled;
            cmd.Parameters.Add("@scheduled", SqlDbType.Int).Value = (int)TutoringSessionStatus.Scheduled;
            cmd.Parameters.Add("@accepted", SqlDbType.Int).Value = (int)TutoringSessionStatus.Accepted;
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = sessionId;

            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            _log.LogInformation("CancelSession #{Id} -> rows {Rows}", sessionId, rows);
            return rows;
        }

        /// <summary>
        /// Mark an Accepted/Completed session as Paid (sets PaidDate=UTC now).
        /// </summary>
        public async Task<int> MarkSessionPaidAsync(int sessionId)
        {
            const string sql = @"
UPDATE dbo.TutoringSession
SET Status = @paid,
    PaidDate = SYSUTCDATETIME()
WHERE TutoringSessionID = @id
  AND Status IN (@accepted, @completed);";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@paid", SqlDbType.Int).Value = (int)TutoringSessionStatus.Paid;
            cmd.Parameters.Add("@accepted", SqlDbType.Int).Value = (int)TutoringSessionStatus.Accepted;
            cmd.Parameters.Add("@completed", SqlDbType.Int).Value = (int)TutoringSessionStatus.Completed;
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = sessionId;

            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            _log.LogInformation("MarkSessionPaid #{Id} -> rows {Rows}", sessionId, rows);
            return rows;
        }

        // ============================================================================
        // SECTION: Light server diagnostics for Agenda (optional)
        // ============================================================================

        /// <summary>
        /// Quick headcount of blocks/sessions in a window (for debug UI/logs).
        /// </summary>
        public async Task<(int blocks, int sessions)> GetWindowDiagnosticsAsync(DateTime fromInclusive, DateTime toExclusive)
        {
            const string SQL_BLOCKS = @"
SELECT COUNT(*) FROM dbo.AvailabilityBlock
WHERE CAST(BlockDate AS date) >= @from AND CAST(BlockDate AS date) < @to;";

            const string SQL_SESSIONS = @"
SELECT COUNT(*) FROM dbo.TutoringSession
WHERE SessionDate >= @from AND SessionDate < @to;";

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            int blocks, sessions;

            await using (var cmd = new SqlCommand(SQL_BLOCKS, conn))
            {
                cmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Date;
                cmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Date;
                blocks = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            await using (var cmd = new SqlCommand(SQL_SESSIONS, conn))
            {
                cmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Date;
                cmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Date;
                sessions = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            _log.LogInformation("[AgendaDiag] Window {From:yyyy-MM-dd}..{To:yyyy-MM-dd}: blocks={Blocks}, sessions={Sessions}",
                fromInclusive, toExclusive, blocks, sessions);

            return (blocks, sessions);
        }
    }
}
