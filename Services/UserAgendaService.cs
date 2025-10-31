#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.ViewModels;
using J_Tutors_Web_Platform.Models.Shared;

namespace J_Tutors_Web_Platform.Services
{
    /// <summary>
    /// Read-only user agenda service (ADO.NET).
    /// Users can view a month calendar of THEIR sessions and view minimal details for a selected session.
    ///
    /// Tables used:
    ///   - TutoringSession (
    ///       TutoringSessionID INT PK,
    ///       UserID INT,
    ///       AdminID INT,
    ///       SubjectID INT,
    ///       SessionDate DATE,
    ///       StartTime TIME,
    ///       DurationHours DECIMAL(4,2),
    ///       BaseCost DECIMAL(10,2),
    ///       PointsSpent INT,
    ///       Status NVARCHAR(...) -- stored as string values like 'Requested','Accepted','Paid','Cancelled'
    ///       ... PaidDate, CancellationDate (not needed here)
    ///     )
    ///
    /// Connection string name: "AzureSql"
    /// </summary>
    public sealed class UserAgendaService
    {
        private readonly IConfiguration _config;
        private const string ConnName = "AzureSql";

        public UserAgendaService(IConfiguration config) => _config = config;

        // ------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------

        /// <summary>
        /// Returns the current user's sessions for the given month.
        /// Set includeRequested=false to hide 'Requested' rows server-side.
        /// </summary>
        public async Task<IReadOnlyList<TutoringSession>> GetUserSessionsForCalendarAsync(
            int userId, int year, int month, bool includeRequested)
        {
            var first = new DateTime(year, month, 1);
            var next = first.AddMonths(1);

            const string sql = @"
SELECT *
FROM TutoringSession
WHERE SessionDate >= @from AND SessionDate < @to
  AND UserID = @uid
  AND (@include = 1 OR Status <> 'Requested')
ORDER BY SessionDate ASC, StartTime ASC;";

            var p = new Dictionary<string, object?>
            {
                ["@from"] = first.Date,
                ["@to"] = next.Date,
                ["@uid"] = userId,
                ["@include"] = includeRequested ? 1 : 0
            };

            await using var con = await OpenAsync();
            using var cmd = BuildCommand(con, sql, p);
            return await QueryListAsync(cmd, MapTutoringSession);
        }

        /// <summary>
        /// Returns minimal, read-only details for a single session that belongs to the given user.
        /// Fields: Status, Date, Start, Duration, FinalRand, PointsSpent.
        /// </summary>
        public async Task<UserSessionDetailsVM?> GetUserSessionDetailsAsync(int userId, int sessionId)
        {
            const string sql = @"
SELECT TOP 1
    TutoringSessionID,
    Status,
    SessionDate,
    StartTime,
    DurationHours,
    BaseCost,
    PointsSpent
FROM TutoringSession
WHERE TutoringSessionID = @sid AND UserID = @uid;";

            var p = new Dictionary<string, object?>
            {
                ["@sid"] = sessionId,
                ["@uid"] = userId
            };

            await using var con = await OpenAsync();
            using var cmd = BuildCommand(con, sql, p);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            // Defensive reads
            int GetInt(string name) => r[name] is DBNull ? 0 : Convert.ToInt32(r[name], CultureInfo.InvariantCulture);
            decimal GetDec(string name) => r[name] is DBNull ? 0m : Convert.ToDecimal(r[name], CultureInfo.InvariantCulture);
            DateTime GetDateTime(string name) => r[name] is DBNull ? DateTime.MinValue : Convert.ToDateTime(r[name], CultureInfo.InvariantCulture);
            TimeSpan GetTimeSpan(string name)
            {
                var v = r[name];
                if (v is DBNull) return TimeSpan.Zero;
                if (v is TimeSpan ts) return ts;
                if (v is DateTime dt) return dt.TimeOfDay;
                return TimeSpan.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out var parsed) ? parsed : TimeSpan.Zero;
            }

            var baseCost = GetDec("BaseCost");
            var points = GetInt("PointsSpent");
            var final = baseCost - points;
            if (final < 0) final = 0;

            return new UserSessionDetailsVM
            {
                TutoringSessionID = GetInt(nameof(UserSessionDetailsVM.TutoringSessionID)),
                Status = Convert.ToString(r["Status"]) ?? "",
                SessionDate = DateOnly.FromDateTime(GetDateTime("SessionDate")),
                StartTime = GetTimeSpan("StartTime"),
                DurationHours = GetDec("DurationHours"),
                PointsSpent = points,
                FinalRand = final
            };
        }

        // ------------------------------------------------------------
        // ADO.NET helpers (local to this service)
        // ------------------------------------------------------------

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
                    cmd.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
                }
            }
            return cmd;
        }

        private static async Task<IReadOnlyList<T>> QueryListAsync<T>(SqlCommand cmd, Func<IDataRecord, T> map)
        {
            var list = new List<T>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(map(reader));
            }
            return list;
        }

        // ------------------------------------------------------------
        // Mapping
        // ------------------------------------------------------------

        /// <summary>
        /// Maps a data record to your EF model class TutoringSession.
        /// Only fields used by the calendar are populated (others remain default).
        /// </summary>
        private static TutoringSession MapTutoringSession(IDataRecord rec)
        {
            object V(string name) => rec[name];

            int GetInt(string name) => V(name) is DBNull ? 0 : Convert.ToInt32(V(name), CultureInfo.InvariantCulture);
            decimal GetDec(string name) => V(name) is DBNull ? 0m : Convert.ToDecimal(V(name), CultureInfo.InvariantCulture);
            DateTime GetDateTime(string name) => V(name) is DBNull ? DateTime.MinValue : Convert.ToDateTime(V(name), CultureInfo.InvariantCulture);
            TimeSpan GetTimeSpan(string name)
            {
                var v = V(name);
                if (v is DBNull) return TimeSpan.Zero;
                if (v is TimeSpan ts) return ts;
                if (v is DateTime dt) return dt.TimeOfDay;
                return TimeSpan.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out var parsed) ? parsed : TimeSpan.Zero;
            }

            var ts = new TutoringSession
            {
                TutoringSessionID = GetInt(nameof(TutoringSession.TutoringSessionID)),
                UserID = GetInt(nameof(TutoringSession.UserID)),
                AdminID = GetInt(nameof(TutoringSession.AdminID)),
                SubjectID = GetInt(nameof(TutoringSession.SubjectID)),
                SessionDate = DateOnly.FromDateTime(GetDateTime(nameof(TutoringSession.SessionDate))),
                StartTime = GetTimeSpan(nameof(TutoringSession.StartTime)),
                DurationHours = GetDec(nameof(TutoringSession.DurationHours)),
                BaseCost = GetDec(nameof(TutoringSession.BaseCost)),
                PointsSpent = GetInt(nameof(TutoringSession.PointsSpent))
            };

            // Status saved as string in DB; translate to enum when possible
            var statusStr = Convert.ToString(V("Status")) ?? "Accepted";
            try
            {
                ts.Status = Enum.Parse<TutoringSessionStatus>(statusStr, ignoreCase: true);
            }
            catch
            {
                ts.Status = TutoringSessionStatus.Accepted; // safe fallback
            }

            return ts;
        }
    }
}
