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
    /// - Availability (Slots): list/create/delete
    /// - Inbox buckets: Scheduled/Accepted/Paid/Cancelled
    /// - Calendar: sessions for [year, month]
    /// 
    /// Connection: ConnectionStrings:azursql
    /// </summary>
    public sealed class AdminAgendaService
    {
        private readonly IConfiguration _config;
        private const string ConnName = "Azuresql";

        public AdminAgendaService(IConfiguration config)
        {
            _config = config;
        }

        // --------------------------------------------------------------------
        // SLOTS / AVAILABILITY
        // --------------------------------------------------------------------
        // AVAILABILITY (uses AvailabilityBlock table)
        public async Task<IReadOnlyList<AvailabilityBlock>> GetAvailabilityBlocksAsync(
            DateTime? fromInclusive = null,
            DateTime? toExclusive = null,
            int? adminId = null)
        {
            // NOTE: BlockDate is a date; StartTime/EndTime are timespans in your model.
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
                ["@from"] = (object?)fromInclusive?.Date ?? DBNull.Value,
                ["@to"] = (object?)toExclusive?.Date ?? DBNull.Value,
            };

            return await QueryListAsync<AvailabilityBlock>(sql, p);
        }

        public async Task<int> CreateAvailabilityBlockAsync(
    int adminId,
    DateTime date,
    TimeSpan start,
    int durationMinutes)
        {
            // EndTime = StartTime + duration
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
                Scheduled = await QuerySessionsByStatusAsync(adminId, "Scheduled"),
                Accepted = await QuerySessionsByStatusAsync(adminId, "Accepted"),
                Paid = await QuerySessionsByStatusAsync(adminId, "Paid"),
                Cancelled = await QuerySessionsByStatusAsync(adminId, "Cancelled"),
            };
        }

        private async Task<IReadOnlyList<TutoringSession>> QuerySessionsByStatusAsync(int? adminId, string status)
        {
            // Keep it simple; your AdminService uses "select * from TutoringSession"
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
    bool includeScheduled,
    int? adminId)
        {
            var first = new DateTime(year, month, 1);
            var next = first.AddMonths(1);

            const string sql = @"
SELECT *
FROM TutoringSession
WHERE SessionDate >= @from AND SessionDate < @to
  AND (@adminId IS NULL OR AdminID = @adminId)
  AND (@include = 1 OR Status <> 'Scheduled')
ORDER BY SessionDate ASC;";

            var p = new Dictionary<string, object?>
            {
                ["@from"] = first.Date,
                ["@to"] = next.Date,
                ["@adminId"] = (object?)adminId ?? DBNull.Value,
                ["@include"] = includeScheduled ? 1 : 0
            };

            return await QueryListAsync<TutoringSession>(sql, p);
        }

        // ====================================================================
        // ADO.NET HELPERS (shared, safe, minimal)
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
            var cmd = new SqlCommand(sql, con)
            {
                CommandType = CommandType.Text
            };

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
            if (result == null || result is DBNull)
                return default!;
            return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
        }

        private async Task<IReadOnlyList<T>> QueryListAsync<T>(string sql, IDictionary<string, object?>? parameters = null) where T : new()
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
        /// Reflection-based, tolerant mapper: populates any writable property on T whose name
        /// matches a column name (case-insensitive), handling common type conversions.
        /// </summary>
        private static T MapRecordTo<T>(IDataRecord record) where T : new()
        {
            var t = new T();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Build a column name → ordinal map (case-insensitive)
            var colOrd = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < record.FieldCount; i++)
            {
                var name = record.GetName(i);
                if (!colOrd.ContainsKey(name))
                    colOrd[name] = i;
            }

            foreach (var p in props)
            {
                if (!p.CanWrite) continue;
                if (!colOrd.TryGetValue(p.Name, out var ordinal)) continue;

                var val = record.IsDBNull(ordinal) ? null : record.GetValue(ordinal);
                if (val == null)
                {
                    p.SetValue(t, null);
                    continue;
                }

                try
                {
                    var target = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

                    if (target == typeof(TimeSpan) && val is TimeSpan ts)
                    {
                        p.SetValue(t, ts);
                    }
                    else if (target == typeof(DateTime) && val is DateTime dt)
                    {
                        p.SetValue(t, DateTime.SpecifyKind(dt, DateTimeKind.Utc)); // assume UTC for DB times
                    }
                    else if (target.IsEnum)
                    {
                        // Try to map string or int to enum
                        if (val is string s)
                            p.SetValue(t, Enum.Parse(target, s, ignoreCase: true));
                        else
                            p.SetValue(t, Enum.ToObject(target, Convert.ToInt32(val, CultureInfo.InvariantCulture)));
                    }
                    else
                    {
                        var converted = Convert.ChangeType(val, target, CultureInfo.InvariantCulture);
                        p.SetValue(t, converted);
                    }
                    // ... inside the try { } that handles conversions:

                    // DateOnly (SessionDate)
                    if (target == typeof(DateOnly))
                    {
                        if (val is DateOnly donly) { p.SetValue(t, donly); }
                        else if (val is DateTime dt) { p.SetValue(t, DateOnly.FromDateTime(dt)); }
                        else if (val is string s && DateTime.TryParse(s, out var pdt)) { p.SetValue(t, DateOnly.FromDateTime(pdt)); }
                        continue;
                    }

                    // TimeOnly (if you ever add in models)
                    if (target == typeof(TimeOnly))
                    {
                        if (val is TimeOnly tonly) { p.SetValue(t, tonly); }
                        else if (val is TimeSpan tsp) { p.SetValue(t, TimeOnly.FromTimeSpan(tsp)); }
                        else if (val is DateTime dt) { p.SetValue(t, TimeOnly.FromDateTime(dt)); }
                        continue;
                    }

                    // Existing cases: TimeSpan, DateTime, enums, etc...

                }
                catch
                {
                    // Tolerate mismatches; leave property at default
                }
            }
            return t;
        }


    }
}
