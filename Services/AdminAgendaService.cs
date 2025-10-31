#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.ViewModels;

namespace J_Tutors_Web_Platform.Services
{
    /// <summary>
    /// Admin Agenda service (SQL / ADO.NET, no EF).
    /// Tables:
    ///  - AvailabilityBlock(AvailabilityBlockID, AdminID, BlockDate, StartTime, EndTime)
    ///  - TutoringSession(..., SessionDate, Status, AdminID, ...)
    /// 
    /// Connection: ConnectionStrings:AzureSql
    /// </summary>
    public sealed class AdminAgendaService
    {
        private readonly IConfiguration _config;
        private const string ConnName = "AzureSql"; // matches your appsettings.json
        private readonly PointsService _points;     // <-- add

        public AdminAgendaService(IConfiguration config, PointsService points) // <-- add param
        {
            _config = config;
            _points = points;                       // <-- add
        }


        // --------------------------------------------------------------------
        // SLOTS / AVAILABILITY
        // --------------------------------------------------------------------
        public async Task<IReadOnlyList<AvailabilityBlock>> GetAvailabilityBlocksAsync(
            DateTime? fromInclusive = null,
            DateTime? toExclusive = null,
            int? adminId = null)
        {
            const string sql = @"
SELECT
    AvailabilityBlockID,
    AdminID,
    BlockDate,
    StartTime,
    EndTime
FROM AvailabilityBlock
WHERE
    (@adminId IS NULL OR AdminID = @adminId)
    AND (@from IS NULL OR BlockDate >= @from)
    AND (@to   IS NULL OR BlockDate <  @to)
ORDER BY BlockDate ASC, StartTime ASC;";

            var p = new Dictionary<string, object?>
            {
                ["@adminId"] = (object?)adminId ?? DBNull.Value,
                ["@from"] = (object?)(fromInclusive?.Date) ?? DBNull.Value,
                ["@to"] = (object?)(toExclusive?.Date) ?? DBNull.Value,
            };

            return await QueryListAsync<AvailabilityBlock>(sql, p);
        }

        public async Task<int> CreateAvailabilityBlockAsync(
            int adminId,
            DateTime date,
            TimeSpan start,
            int durationMinutes)
        {
            var end = start.Add(TimeSpan.FromMinutes(durationMinutes));

            const string sql = @"
INSERT INTO AvailabilityBlock (AdminID, BlockDate, StartTime, EndTime)
OUTPUT INSERTED.AvailabilityBlockID
VALUES (@AdminID, @BlockDate, @StartTime, @EndTime);";

            var p = new Dictionary<string, object?>
            {
                ["@AdminID"] = adminId,
                ["@BlockDate"] = date.Date,
                ["@StartTime"] = start,
                ["@EndTime"] = end
            };

            return await ExecuteScalarAsync<int>(sql, p);
        }

        public async Task<int> DeleteAvailabilityBlockAsync(int id)
        {
            const string sql = @"DELETE FROM AvailabilityBlock WHERE AvailabilityBlockID = @id;";
            var p = new Dictionary<string, object?> { ["@id"] = id };
            return await ExecuteNonQueryAsync(sql, p);
        }

        // --------------------------------------------------------------------
        // INBOX
        // --------------------------------------------------------------------
        public async Task<AgendaInboxVM> GetInboxAsync(int? adminId)
        {
            return new AgendaInboxVM
            {
                Requested = await QuerySessionsByStatusAsync(adminId, "Requested"),
                Accepted = await QuerySessionsByStatusAsync(adminId, "Accepted"),
                Paid = await QuerySessionsByStatusAsync(adminId, "Paid"),
                Cancelled = await QuerySessionsByStatusAsync(adminId, "Cancelled"),
            };
        }

        private async Task<IReadOnlyList<TutoringSession>> QuerySessionsByStatusAsync(int? adminId, string status)
        {
            const string sql = @"
SELECT *
FROM TutoringSession
WHERE Status = @status
  AND (@adminId IS NULL OR AdminID = @adminId)
ORDER BY SessionDate DESC;";

            var p = new Dictionary<string, object?>
            {
                ["@status"] = status,
                ["@adminId"] = (object?)adminId ?? DBNull.Value
            };

            return await QueryListAsync<TutoringSession>(sql, p);
        }

        // --------------------------------------------------------------------
        // CALENDAR
        // --------------------------------------------------------------------
        public async Task<IReadOnlyList<TutoringSession>> GetSessionsForCalendarAsync(
            int year,
            int month,
            bool includeRequested,
            int? adminId)
        {
            var first = new DateTime(year, month, 1);
            var next = first.AddMonths(1);

            const string sql = @"
SELECT *
FROM TutoringSession
WHERE SessionDate >= @from AND SessionDate < @to
  AND (@adminId IS NULL OR AdminID = @adminId)
  AND (@include = 1 OR Status <> 'Requested')
ORDER BY SessionDate ASC;";

            var p = new Dictionary<string, object?>
            {
                ["@from"] = first.Date,
                ["@to"] = next.Date,
                ["@adminId"] = (object?)adminId ?? DBNull.Value,
                ["@include"] = includeRequested ? 1 : 0
            };

            return await QueryListAsync<TutoringSession>(sql, p);
        }

        // ====================================================================
        // ADO.NET HELPERS
        // ====================================================================
        private string GetConnectionString()
            => _config.GetConnectionString(ConnName)
               ?? throw new InvalidOperationException($"Connection string '{ConnName}' not found.");

        private async Task<SqlConnection> OpenAsync()
        {
            var con = new SqlConnection(GetConnectionString());
            await con.OpenAsync();
            return con;
        }

        private static SqlCommand BuildCommand(SqlConnection con, string sql, IDictionary<string, object?>? parameters = null)
        {
            var cmd = new SqlCommand(sql, con) { CommandType = CommandType.Text };
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    var value = kvp.Value ?? DBNull.Value;
                    cmd.Parameters.AddWithValue(kvp.Key, value);
                }
            }
            return cmd;
        }

        private async Task<int> ExecuteNonQueryAsync(string sql, IDictionary<string, object?>? parameters = null)
        {
            using var con = await OpenAsync();
            using var cmd = BuildCommand(con, sql, parameters);
            return await cmd.ExecuteNonQueryAsync();
        }

        private async Task<T> ExecuteScalarAsync<T>(string sql, IDictionary<string, object?>? parameters = null)
        {
            using var con = await OpenAsync();
            using var cmd = BuildCommand(con, sql, parameters);
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result is DBNull) return default!;
            return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
        }

        private async Task<IReadOnlyList<T>> QueryListAsync<T>(string sql, IDictionary<string, object?>? parameters = null)
            where T : new()
        {
            using var con = await OpenAsync();
            using var cmd = BuildCommand(con, sql, parameters);
            using var reader = await cmd.ExecuteReaderAsync();

            var list = new List<T>();
            while (await reader.ReadAsync())
            {
                list.Add(MapRecordTo<T>(reader));
            }
            return list;
        }

        /// <summary>
        /// Reflection-based mapper. IMPORTANT: handle DateOnly/TimeOnly BEFORE generic Convert.ChangeType.
        /// </summary>
        private static T MapRecordTo<T>(IDataRecord record) where T : new()
        {
            var t = new T();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // column name → ordinal
            var colOrd = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < record.FieldCount; i++)
            {
                var name = record.GetName(i);
                if (!colOrd.ContainsKey(name)) colOrd[name] = i;
            }

            foreach (var p in props)
            {
                if (!p.CanWrite) continue;
                if (!colOrd.TryGetValue(p.Name, out var ordinal)) continue;

                var isNull = record.IsDBNull(ordinal);
                var val = isNull ? null : record.GetValue(ordinal);

                try
                {
                    var target = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

                    if (val == null)
                    {
                        p.SetValue(t, null);
                        continue;
                    }

                    // First: special cases for DateOnly / TimeOnly / TimeSpan / DateTime / Enums
                    if (target == typeof(DateOnly))
                    {
                        if (val is DateOnly donly) p.SetValue(t, donly);
                        else if (val is DateTime dt) p.SetValue(t, DateOnly.FromDateTime(dt));
                        else if (val is string s && DateTime.TryParse(s, out var pdt)) p.SetValue(t, DateOnly.FromDateTime(pdt));
                        continue;
                    }

                    if (target == typeof(TimeOnly))
                    {
                        if (val is TimeOnly tonly) p.SetValue(t, tonly);
                        else if (val is TimeSpan tsp) p.SetValue(t, TimeOnly.FromTimeSpan(tsp));
                        else if (val is DateTime dt) p.SetValue(t, TimeOnly.FromDateTime(dt));
                        continue;
                    }

                    if (target == typeof(TimeSpan) && val is TimeSpan ts)
                    {
                        p.SetValue(t, ts);
                        continue;
                    }

                    if (target == typeof(DateTime) && val is DateTime dt2)
                    {
                        p.SetValue(t, DateTime.SpecifyKind(dt2, DateTimeKind.Utc));
                        continue;
                    }

                    if (target.IsEnum)
                    {
                        if (val is string es) p.SetValue(t, Enum.Parse(target, es, ignoreCase: true));
                        else p.SetValue(t, Enum.ToObject(target, Convert.ToInt32(val, CultureInfo.InvariantCulture)));
                        continue;
                    }

                    // Fallback: generic conversion
                    var converted = Convert.ChangeType(val, target, CultureInfo.InvariantCulture);
                    p.SetValue(t, converted);
                }
                catch
                {
                    // swallow & leave default
                }
            }
            return t;
        }
        private async Task<IReadOnlyList<AgendaInboxRowVM>> QueryInboxRowsByStatusAsync(int? adminId, string status)
        {
            // NOTE: Status is stored as a string in DB (per your confirmation).
            const string sql = @"SELECT ts.TutoringSessionID, s.SubjectName, ts.DurationHours, (u.FirstName + ' ' + u.Surname) AS RequestingFullName, ts.BaseCost, ts.PointsSpent, ts.Status FROM TutoringSession ts JOIN Users u ON ts.UserID   = u.UserID JOIN Subjects s  ON ts.SubjectID = s.SubjectID WHERE ts.Status = @status AND (@adminId IS NULL OR ts.AdminID = @adminId) ORDER BY ts.SessionDate ASC, ts.StartTime ASC;";

            var p = new Dictionary<string, object?>
            {
                ["@status"] = status,
                ["@adminId"] = (object?)adminId ?? DBNull.Value
            };

            // Manual projection (don’t use reflection mapper here; types differ)
            var list = new List<AgendaInboxRowVM>();
            using var con = await OpenAsync();
            using var cmd = BuildCommand(con, sql, p);
            using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                list.Add(new AgendaInboxRowVM
                {
                    TutoringSessionID = r.GetInt32(r.GetOrdinal("TutoringSessionID")),
                    SubjectName = r.GetString(r.GetOrdinal("SubjectName")),
                    DurationHours = (decimal)r.GetDecimal(r.GetOrdinal("DurationHours")),
                    RequestingFullName = r.GetString(r.GetOrdinal("RequestingFullName")),
                    BaseCost = (decimal)r.GetDecimal(r.GetOrdinal("BaseCost")),
                    PointsSpent = r.GetInt32(r.GetOrdinal("PointsSpent")),
                    Status = r.GetString(r.GetOrdinal("Status"))
                });
            }
            return list;
        }

        public async Task<AgendaInboxDisplayVM> GetInboxDisplayAsync(int? adminId)
        {
            return new AgendaInboxDisplayVM
            {
                Requested = await QueryInboxRowsByStatusAsync(adminId, "Requested"),
                Accepted = await QueryInboxRowsByStatusAsync(adminId, "Accepted"),
                Paid = await QueryInboxRowsByStatusAsync(adminId, "Paid"),
                Cancelled = await QueryInboxRowsByStatusAsync(adminId, "Cancelled"),
            };
        }

        public async Task<SessionDetailsVM?> GetSessionDetailsAsync(int sessionId)
        {
            const string sql = @"SELECT TOP 1 ts.TutoringSessionID, ts.Status, s.SubjectName, ts.SessionDate, ts.StartTime, ts.DurationHours, u.UserID, u.FirstName, u.Surname, u.Email, ts.BaseCost, ts.PointsSpent FROM TutoringSession ts JOIN Users u ON ts.UserID = u.UserID JOIN Subjects s  ON ts.SubjectID = s.SubjectID WHERE ts.TutoringSessionID = @id;";

            var p = new Dictionary<string, object?> { ["@id"] = sessionId };

            using var con = await OpenAsync();
            using var cmd = BuildCommand(con, sql, p);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            // project row
            var vm = new SessionDetailsVM
            {
                TutoringSessionID = r.GetInt32(r.GetOrdinal("TutoringSessionID")),
                Status = r.GetString(r.GetOrdinal("Status")),
                SubjectName = r.GetString(r.GetOrdinal("SubjectName")),
                SessionDate = DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("SessionDate"))),
                StartTime = (r["StartTime"] is TimeSpan ts ? ts : TimeSpan.Zero),
                DurationHours = r.GetDecimal(r.GetOrdinal("DurationHours")),
                UserID = r.GetInt32(r.GetOrdinal("UserID")),
                FirstName = r.GetString(r.GetOrdinal("FirstName")),
                Surname = r.GetString(r.GetOrdinal("Surname")),
                Email = r.GetString(r.GetOrdinal("Email")),
                BaseCost = r.GetDecimal(r.GetOrdinal("BaseCost")),
                PointsSpent = r.GetInt32(r.GetOrdinal("PointsSpent")),
            };

            // fill unpaid total
            vm.UnpaidRandForUser = await GetUnpaidRandForUserAsync(vm.UserID);
            return vm;
        }

        public async Task<decimal> GetUnpaidRandForUserAsync(int userId)
        {
            const string sql = @"SELECT COALESCE(SUM(CASE WHEN (BaseCost - PointsSpent) < 0 THEN 0 ELSE (BaseCost - PointsSpent) END), 0) FROM TutoringSession WHERE UserID = @uid AND Status = 'Accepted' AND PaidDate IS NULL;";

            var p = new Dictionary<string, object?> { ["@uid"] = userId };
            return await ExecuteScalarAsync<decimal>(sql, p);
        }

        public async Task<(bool Ok, string Message)> UpdateSessionStatusAsync(int sessionId, string newStatus)
        {
            const string getSql = @"SELECT TutoringSessionID, Status, PaidDate, CancellationDate FROM TutoringSession WHERE TutoringSessionID = @id";

            await using var con = await OpenAsync();

            string currentStatus;
            using (var getCmd = BuildCommand(con, getSql, new Dictionary<string, object?> { ["@id"] = sessionId }))
            using (var r = await getCmd.ExecuteReaderAsync())
            {
                if (!await r.ReadAsync())
                    return (false, "Session not found.");

                currentStatus = r["Status"] as string ?? "";
            }

            bool legal = currentStatus switch
            {
                "Requested" => newStatus is "Accepted" or "Denied",
                "Accepted" => newStatus is "Paid" or "Cancelled",
                _ => false
            };
            if (!legal) return (false, $"Illegal transition: {currentStatus} → {newStatus}");

            string updateSql;
            if (newStatus == "Paid")
            {
                updateSql = @"UPDATE TutoringSession SET Status = @status, PaidDate = SYSUTCDATETIME() WHERE TutoringSessionID = @id;";
            }
            else if (newStatus == "Cancelled")
            {
                updateSql = @"UPDATE TutoringSession SET Status = @status, CancellationDate = SYSUTCDATETIME() WHERE TutoringSessionID = @id;";
            }
            else
            {
                updateSql = @"UPDATE TutoringSession SET Status = @status, PaidDate = NULL, CancellationDate = NULL WHERE TutoringSessionID = @id;";
            }

            await using var tx = await con.BeginTransactionAsync();
            try
            {
                using (var updCmd = new SqlCommand(updateSql, con, (SqlTransaction)tx))
                {
                    updCmd.Parameters.AddWithValue("@status", newStatus);
                    updCmd.Parameters.AddWithValue("@id", sessionId);
                    var rows = await updCmd.ExecuteNonQueryAsync();
                    if (rows <= 0)
                    {
                        await tx.RollbackAsync();
                        return (false, "No changes applied.");
                    }
                }

                if (newStatus == "Denied" || newStatus == "Cancelled")
                {
                    var reference = $"TS-{sessionId}";
                    await _points.DeleteByReference(reference, con, (SqlTransaction)tx);
                }

                await tx.CommitAsync();
                return (true, "Status updated.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }







    }
}
