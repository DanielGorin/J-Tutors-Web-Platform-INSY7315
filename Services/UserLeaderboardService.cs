using System.Data;
using System.Data.SqlTypes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using J_Tutors_Web_Platform.ViewModels;

namespace J_Tutors_Web_Platform.Services
{
    // SERVICE: UserLeaderboardService
    // PURPOSE: Build the leaderboard page VM using raw ADO.NET.
    // NOTES:
    // - No search (UI removed)
    // - Modes: Current | Total
    // - Shows ALL users (even with 0 points)
    public class UserLeaderboardService
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // CONFIG & LOGGING
        // ─────────────────────────────────────────────────────────────────────────────
        private readonly string _connStr;
        private readonly ILogger<UserLeaderboardService> _log;

        public UserLeaderboardService(IConfiguration cfg, ILogger<UserLeaderboardService> log)
        {
            _connStr = cfg.GetConnectionString("AzureSql")!;
            _log = log;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // API: Build leaderboard page VM (no search)
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<LeaderboardPageVM> GetPageAsync(
            string? currentUsername,
            LeaderboardViewMode mode,
            LeaderboardTimeFilter time,
            int page,
            int pageSize)
        {
            // 1) Concrete timeframe (UTC), clamped like AdminUserDirectory
            var (startUtc, endUtc) = ComputeWindowUtc(time);

            // 2) Read ALL users (include those with 0 points)
            var users = new List<(int UserID, string Username)>();
            await using (var conn = new SqlConnection(_connStr))
            await using (var cmd = new SqlCommand(@"SELECT UserID, Username FROM Users", conn))
            {
                await conn.OpenAsync();
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    users.Add((r.GetInt32(0), r.GetString(1)));
                }
            }

            // 3) Single aggregate over window using *Directory math*
            //    PointsTotal   = SUM(CASE WHEN Type IN (0,2) THEN Amount ELSE 0 END)
            //    SpentPositive = SUM(CASE WHEN Type = 1 THEN -Amount ELSE 0 END)
            //    PointsCurrent = PointsTotal - SpentPositive
            var totals = new Dictionary<int, (int PointsTotal, int SpentPositive)>();

            const string sql = @"
WITH W AS (
    SELECT UserID, [Type], Amount
    FROM dbo.PointsReceipt
    WHERE ReceiptDate >= @start AND ReceiptDate < @end
)
SELECT 
    UserID,
    SUM(CASE WHEN [Type] IN (0,2) THEN Amount ELSE 0 END)  AS PointsTotal,
    SUM(CASE WHEN [Type] = 1      THEN -Amount ELSE 0 END) AS SpentPositive
FROM W
GROUP BY UserID;";

            await using (var conn2 = new SqlConnection(_connStr))
            await using (var cmd2 = new SqlCommand(sql, conn2))
            {
                cmd2.Parameters.AddWithValue("@start", startUtc);
                cmd2.Parameters.AddWithValue("@end", endUtc);

                await conn2.OpenAsync();
                await using var r2 = await cmd2.ExecuteReaderAsync();
                while (await r2.ReadAsync())
                {
                    var uid = r2.GetInt32(0);
                    var pt = r2.IsDBNull(1) ? 0 : Convert.ToInt32(r2[1]);
                    var sp = r2.IsDBNull(2) ? 0 : Convert.ToInt32(r2[2]);
                    totals[uid] = (pt, sp);
                }
            }

            // 4) Build rows with unified metrics
            var rows = new List<LeaderboardRowVM>();
            foreach (var u in users)
            {
                var (pt, sp) = totals.TryGetValue(u.UserID, out var t) ? t : (0, 0);
                var current = pt - sp;

                rows.Add(new LeaderboardRowVM
                {
                    UserID = u.UserID,
                    Username = u.Username,

                    // Match your view columns:
                    TotalEarned = pt,          // "Total Points" = Earned + Adjustments (±)
                    EarnedInPeriod = current,  // "Current Points" = Total - SpentPositive

                    // Legacy/compat fields:
                    NetAllTime = 0,
                    EarnedThisMonth = 0,
                    IsCurrentUser = currentUsername != null && u.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase),
                    IsVisibleOptIn = true
                });
            }

            // 5) Sort + stable rank
            rows = (mode == LeaderboardViewMode.Current)
                ? rows.OrderByDescending(x => x.EarnedInPeriod).ThenBy(x => x.Username).ToList()
                : rows.OrderByDescending(x => x.TotalEarned).ThenBy(x => x.Username).ToList();

            var lastMetric = int.MinValue;
            var rank = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var metric = (mode == LeaderboardViewMode.Current) ? rows[i].EarnedInPeriod : rows[i].TotalEarned;
                if (i == 0 || metric != lastMetric)
                {
                    rank = i + 1;
                    lastMetric = metric;
                }
                rows[i].Rank = rank;
            }

            // 6) Paging
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            var total = rows.Count;
            var pageRows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new LeaderboardPageVM
            {
                Filters = new LeaderboardFiltersVM
                {
                    Mode = mode,
                    Time = time,
                    Page = page,
                    PageSize = pageSize
                },
                Rows = pageRows,
                TotalRows = total,
                ShowingFrom = total == 0 ? 0 : ((page - 1) * pageSize + 1),
                ShowingTo = Math.Min(page * pageSize, total)
            };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // HELPER: timeframe calculator (UTC) — concrete bounds for all modes
        // ─────────────────────────────────────────────────────────────────────────────
        private static (DateTime startUtc, DateTime endUtc) ComputeWindowUtc(LeaderboardTimeFilter time)
        {
            // Use concrete bounds (no nulls), and clamp AllTime start to SQL datetime minimum
            // to match AdminUserDirectory and avoid DateTime.MinValue underflow.
            DateTime nowUtc = DateTime.UtcNow;

            static DateTime FirstDayOfThisMonthUtc()
            {
                var todayLocal = DateTime.Today;
                var firstLocal = new DateTime(todayLocal.Year, todayLocal.Month, 1, 0, 0, 0, DateTimeKind.Local);
                return firstLocal.ToUniversalTime();
            }

            switch (time)
            {
                case LeaderboardTimeFilter.ThisMonth:
                    return (FirstDayOfThisMonthUtc(), nowUtc);

                case LeaderboardTimeFilter.Last30Days:
                    return (nowUtc.AddDays(-30), nowUtc);

                case LeaderboardTimeFilter.LastMonth:
                    var todayLocal = DateTime.Today;
                    var firstThis = new DateTime(todayLocal.Year, todayLocal.Month, 1);
                    var lastMonthEndLocal = firstThis.AddDays(-1); // last day of previous month
                    var lastMonthStartLocal = new DateTime(lastMonthEndLocal.Year, lastMonthEndLocal.Month, 1);
                    var startUtc = DateTime.SpecifyKind(lastMonthStartLocal, DateTimeKind.Local).ToUniversalTime();
                    var endUtc = DateTime.SpecifyKind(lastMonthEndLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime(); // exclusive-like end
                    return (startUtc, endUtc);

                case LeaderboardTimeFilter.AllTime:
                default:
                    return (SqlDateTime.MinValue.Value, nowUtc);
            }
        }
    }
}
