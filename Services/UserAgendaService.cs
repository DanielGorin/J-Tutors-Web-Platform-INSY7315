/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * UserAgendaService
 * File Purpose:
 * This is a service that handles admin methods for managing agenda/calender data
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */
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
    public sealed class UserAgendaService
    {
        private readonly IConfiguration _config;
        private const string ConnName = "AzureSql";

        public UserAgendaService(IConfiguration config) => _config = config;

        // ------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------

        // Returns the current user's sessions for the given month.
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

        // Maps a data record to EF model class TutoringSession.
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
