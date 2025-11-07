/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * AdminUserDirectoryService
 * File Purpose:
 * This is a service that handles admin methods for retreiving user info
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using J_Tutors_Web_Platform.ViewModels;
using System.Data.SqlTypes;


namespace J_Tutors_Web_Platform.Services
{
    // ======================================================
    // Helper: timeframe window calculations
    // ======================================================
    internal static class AdminUserDirectoryTimeframeHelper
    {
        public static (DateTime fromUtc, DateTime toUtc) GetWindowUtc(AdminDirectoryTimeframe tf)
        {
            DateTime nowUtc = DateTime.UtcNow;
            DateTime fromUtc;

            switch (tf)
            {
                case AdminDirectoryTimeframe.ThisMonth:
                    fromUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    break;

                case AdminDirectoryTimeframe.Last30Days:
                    fromUtc = nowUtc.AddDays(-30);
                    break;

                case AdminDirectoryTimeframe.LastMonth:
                    var firstOfThis = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    fromUtc = firstOfThis.AddMonths(-1);
                    nowUtc = firstOfThis.AddSeconds(-1); // end of last month
                    break;

                case AdminDirectoryTimeframe.Last4Months:
                    fromUtc = nowUtc.AddMonths(-4);
                    break;

                case AdminDirectoryTimeframe.AllTime:
                default:
                    fromUtc = SqlDateTime.MinValue.Value;
                    break;

            }

            return (fromUtc, nowUtc);
        }
    }

    // ======================================================
    // Admin User Directory Service
    // ======================================================
    public sealed class AdminUserDirectoryService
    {
        private readonly string _connStr;

        public AdminUserDirectoryService(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("AzureSql")
                ?? throw new InvalidOperationException("ConnectionStrings:AzureSql missing.");
        }

        public async Task<AdminUserDirectoryPageViewModel> GetPageAsync(
            string? search,
            AdminDirectoryTimeframe timeframe,
            string sortColumn,
            string sortDirection,
            int page,
            int pageSize)
        {
            var (fromUtc, toUtc) = AdminUserDirectoryTimeframeHelper.GetWindowUtc(timeframe);

            sortColumn = string.IsNullOrWhiteSpace(sortColumn) ? "Username" : sortColumn;
            sortDirection = sortDirection?.ToUpperInvariant() == "DESC" ? "DESC" : "ASC";
            page = page < 1 ? 1 : page;
            pageSize = pageSize <= 0 ? 25 : pageSize;

            var rows = new List<AdminUserDirectoryRowViewModel>();

            using var con = new SqlConnection(_connStr);
            await con.OpenAsync();

            // Optional search clause
            string whereSearch = "";
            if (!string.IsNullOrWhiteSpace(search))
                whereSearch = "WHERE (u.Username LIKE @s OR u.FirstName LIKE @s OR u.Surname LIKE @s)";

            int offset = (page - 1) * pageSize;

            string sql = $@"
            WITH UserData AS (
                SELECT 
                    u.UserID,
                    u.Username,
                    u.FirstName,
                    u.Surname,
                    u.BirthDate,
                    u.LeaderboardVisible,
                    -- Points earned or adjusted in timeframe
                    COALESCE(SUM(CASE 
                    WHEN pr.Type IN (0, 2) THEN pr.Amount   -- Earned + all Adjustments (±), same as PointsService
                    ELSE 0 
                    END), 0) AS PointsTotal,
                    -- Points spent (negative) in timeframe
                    COALESCE(SUM(CASE 
                        WHEN pr.Amount < 0 AND pr.Type = 1 THEN pr.Amount
                        ELSE 0 END), 0) AS PointsSpend
                FROM Users u
                LEFT JOIN PointsReceipt pr ON pr.UserID = u.UserID
                    AND pr.ReceiptDate BETWEEN @from AND @to
                {whereSearch}
                GROUP BY u.UserID, u.Username, u.FirstName, u.Surname, u.BirthDate, u.LeaderboardVisible
            ),
            WithCurrent AS (
                SELECT *,
                       (PointsTotal - PointsSpend) AS PointsCurrent
                FROM UserData
            ),
            WithUnpaid AS (
                SELECT 
                    w.*,
                    COALESCE(SUM(CASE 
                        WHEN s.Status = 'Accepted' AND s.PaidDate IS NULL THEN 
                            CASE WHEN (s.BaseCost - s.PointsSpent) > 0 THEN (s.BaseCost - s.PointsSpent) ELSE 0 END
                        ELSE 0 END),0) AS UnpaidRandTotal
                FROM WithCurrent w
                LEFT JOIN TutoringSession s ON s.UserID = w.UserID
                GROUP BY w.UserID, w.Username, w.FirstName, w.Surname, w.BirthDate, w.LeaderboardVisible, w.PointsTotal, w.PointsSpend, w.PointsCurrent
            )
            SELECT * FROM WithUnpaid
            ORDER BY {sortColumn} {sortDirection}
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;

            SELECT COUNT(*) FROM Users u {whereSearch};
            ";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@from", fromUtc);
            cmd.Parameters.AddWithValue("@to", toUtc);
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@s", $"%{search}%");

            using var reader = await cmd.ExecuteReaderAsync();

            // --- Read user rows ---
            while (await reader.ReadAsync())
            {
                var r = new AdminUserDirectoryRowViewModel
                {
                    UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    Surname = reader.GetString(reader.GetOrdinal("Surname")),
                    BirthDate = reader.IsDBNull(reader.GetOrdinal("BirthDate"))
                        ? DateTime.MinValue
                        : reader.GetDateTime(reader.GetOrdinal("BirthDate")),
                    LeaderboardVisible = reader.GetBoolean(reader.GetOrdinal("LeaderboardVisible")),
                    PointsTotal = reader.GetInt32(reader.GetOrdinal("PointsTotal")),
                    PointsCurrent = reader.GetInt32(reader.GetOrdinal("PointsCurrent")),
                    UnpaidRandTotal = Convert.ToDecimal(reader["UnpaidRandTotal"])
                };
                rows.Add(r);
            }

            // --- Read total count ---
            await reader.NextResultAsync();
            int totalRows = 0;
            if (await reader.ReadAsync())
                totalRows = reader.GetInt32(0);

            await reader.CloseAsync();

            return new AdminUserDirectoryPageViewModel
            {
                Search = search,
                Timeframe = timeframe,
                SortColumn = sortColumn,
                SortDirection = sortDirection,
                Page = page,
                PageSize = pageSize,
                TotalRows = totalRows,
                Rows = rows
            };
        }

        public async Task<AdminUserDetailsViewModel?> GetUserBasicsAsync(int userId)
        {
            const string sql = @"
SELECT TOP 1
    u.UserID,
    u.Username,
    u.FirstName,
    u.Surname,
    u.Email,
    u.Phone,
    u.SubjectInterest,
    u.LeaderboardVisible,
    u.ThemePreference,
    u.BirthDate,
    u.RegistrationDate
FROM Users u
WHERE u.UserID = @id;";

            await using var con = new SqlConnection(_connStr);
            await con.OpenAsync();

            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new AdminUserDetailsViewModel
            {
                UserID = r.GetInt32(r.GetOrdinal("UserID")),
                Username = r.GetString(r.GetOrdinal("Username")),
                FirstName = r.GetString(r.GetOrdinal("FirstName")),
                Surname = r.GetString(r.GetOrdinal("Surname")),
                Email = r["Email"] as string,
                Phone = r["Phone"] as string,
                SubjectInterest = r["SubjectInterest"] as string,
                LeaderboardVisible = r.GetBoolean(r.GetOrdinal("LeaderboardVisible")),
                ThemePreference = r["ThemePreference"] as string,
                BirthDate = r.IsDBNull(r.GetOrdinal("BirthDate")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("BirthDate")),
                RegistrationDate = r.IsDBNull(r.GetOrdinal("RegistrationDate")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("RegistrationDate"))
            };
        }
    }
}
