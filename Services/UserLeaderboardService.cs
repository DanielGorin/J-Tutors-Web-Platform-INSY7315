using System.Data;
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
            // SUB-SEGMENT timeframe (UTC)
            var (startUtc, endUtc) = ComputeWindowUtc(time);

            // SUB-SEGMENT read ALL users (include even those with 0 points)
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

            // SUB-SEGMENT aggregate: Total points in window (sum of positive earns)
            // Definition: SUM(Amount) WHERE Amount > 0 AND within [start, end)
            var totalInPeriod = new Dictionary<int, int>(); // UserID -> sum
            await using (var conn = new SqlConnection(_connStr))
            {
                var sql = @"SELECT UserID, SUM(Amount) AS TotalEarned FROM dbo.PointsReceipt WHERE Amount > 0" + 
                    (startUtc.HasValue ? "AND ReceiptDate >= @start " : "") + (endUtc.HasValue ? "AND ReceiptDate <  @end " : "") + @"GROUP BY UserID";

                await using var cmd = new SqlCommand(sql, conn);
                if (startUtc.HasValue) cmd.Parameters.AddWithValue("@start", startUtc.Value);
                if (endUtc.HasValue) cmd.Parameters.AddWithValue("@end", endUtc.Value);

                await conn.OpenAsync();
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var uid = r.GetInt32(0);
                    var sum = r.IsDBNull(1) ? 0 : Convert.ToInt32(r[1]);
                    totalInPeriod[uid] = sum;
                }
            }

            // SUB-SEGMENT aggregate: Current points in window (net affecting balance)
            // Definition: SUM(Amount) WHERE AffectsAllTime = 1 AND within [start, end)
            var currentInPeriod = new Dictionary<int, int>(); // UserID -> net
            await using (var conn = new SqlConnection(_connStr))
            {
                var sql = @"SELECT UserID, SUM(Amount) AS Net FROM dbo.PointsReceipt WHERE AffectsAllTime = 1 
                    " + (startUtc.HasValue ? "AND ReceiptDate >= @start " : "") +
                    (endUtc.HasValue ? "AND ReceiptDate <  @end " : "") + @"GROUP BY UserID";

                await using var cmd = new SqlCommand(sql, conn);
                if (startUtc.HasValue) cmd.Parameters.AddWithValue("@start", startUtc.Value);
                if (endUtc.HasValue) cmd.Parameters.AddWithValue("@end", endUtc.Value);

                await conn.OpenAsync();
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var uid = r.GetInt32(0);
                    var sum = r.IsDBNull(1) ? 0 : Convert.ToInt32(r[1]);
                    currentInPeriod[uid] = sum;
                }
            }

            // SUB-SEGMENT build rows (no visibility filter)
            var rows = new List<LeaderboardRowVM>();
            foreach (var u in users)
            {
                rows.Add(new LeaderboardRowVM
                {
                    UserID = u.UserID,
                    Username = u.Username,

                    // New columns the view needs:
                    TotalEarned = totalInPeriod.TryGetValue(u.UserID, out var te) ? te : 0,  // "Total Points"
                    EarnedInPeriod = currentInPeriod.TryGetValue(u.UserID, out var cp) ? cp : 0, // "Current Points"

                    // Legacy fields kept for compatibility (not used in the simplified table):
                    NetAllTime = 0,
                    EarnedThisMonth = 0,
                    IsCurrentUser = currentUsername != null && u.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase),
                    IsVisibleOptIn = true
                });
            }

            // SUB-SEGMENT sort + rank (stable by Username for ties)
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

            // SUB-SEGMENT paging (1-based)
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
        // HELPER: timeframe calculator (UTC)
        // ─────────────────────────────────────────────────────────────────────────────
        private static (DateTime? startUtc, DateTime? endUtc) ComputeWindowUtc(LeaderboardTimeFilter time)
        {
            var nowUtc = DateTime.UtcNow;

            DateTime FirstDayOfThisMonthUtc()
            {
                var todayLocal = DateTime.Today;
                var firstLocal = new DateTime(todayLocal.Year, todayLocal.Month, 1, 0, 0, 0, DateTimeKind.Local);
                return firstLocal.ToUniversalTime();
            }

            switch (time)
            {
                case LeaderboardTimeFilter.ThisMonth:
                    return (FirstDayOfThisMonthUtc(), null);
                case LeaderboardTimeFilter.Last30Days:
                    return (nowUtc.AddDays(-30), null);
                case LeaderboardTimeFilter.LastMonth:
                    var todayLocal = DateTime.Today;
                    var firstThis = new DateTime(todayLocal.Year, todayLocal.Month, 1);
                    var lastMonthEndLocal = firstThis.AddDays(-1);
                    var lastMonthStartLocal = new DateTime(lastMonthEndLocal.Year, lastMonthEndLocal.Month, 1);
                    var startUtc = DateTime.SpecifyKind(lastMonthStartLocal, DateTimeKind.Local).ToUniversalTime();
                    var endUtc = DateTime.SpecifyKind(lastMonthEndLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime(); // exclusive
                    return (startUtc, endUtc);
                case LeaderboardTimeFilter.AllTime:
                default:
                    return (null, null);
            }
        }
    }
}
