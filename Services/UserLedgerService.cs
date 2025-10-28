using J_Tutors_Web_Platform.Models.Points;
using J_Tutors_Web_Platform.Models.Shared;
using Microsoft.Data.SqlClient;

namespace J_Tutors_Web_Platform.Services
{
    // SERVICE: UserLedgerService
    // PURPOSE: Handles all DB logic for the Points Ledger page.
    public class UserLedgerService
    {
        private readonly string _connStr;
        private readonly ILogger<UserLedgerService> _log;

        public UserLedgerService(IConfiguration cfg, ILogger<UserLedgerService> log)
        {
            _connStr = cfg.GetConnectionString("AzureSql")!;
            _log = log;
        }

        // ─────────────────────────────────────────────────────────────
        // READ: All receipts for a user (newest → oldest)
        // ─────────────────────────────────────────────────────────────
        public async Task<List<PointsReceipt>> GetReceiptsForUserAsync(int userId)
        {
            var list = new List<PointsReceipt>();
            var sql = @"SELECT * FROM dbo.PointsReceipt WHERE UserID = @id ORDER BY ReceiptDate DESC";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", userId);

            await conn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new PointsReceipt
                {
                    PointsReceiptID = r.GetInt32(0),
                    ReceiptDate = r.GetDateTime(1),
                    Type = (PointsReceiptType)r.GetInt32(2),
                    Amount = r.GetInt32(3),
                    Reason = r["Reason"] as string,
                    Reference = r["Reference"] as string
                });
            }

            return list;
        }

        // ─────────────────────────────────────────────────────────────
        // READ: Totals (earned, deducted, balance)
        // ─────────────────────────────────────────────────────────────
        public async Task<(int earned, int deducted, int balance)> GetTotalsForUserAsync(int userId)
        {
            var sql = @"SELECT SUM(CASE WHEN Amount > 0 THEN Amount ELSE 0 END) AS Earned, SUM(CASE WHEN Amount < 0 THEN -Amount ELSE 0 END) AS Deducted, SUM(Amount) AS Balance FROM dbo.PointsReceipt WHERE UserID = @id";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", userId);

            await conn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return (
                    r.IsDBNull(0) ? 0 : r.GetInt32(0),
                    r.IsDBNull(1) ? 0 : r.GetInt32(1),
                    r.IsDBNull(2) ? 0 : r.GetInt32(2)
                );
            }
            return (0, 0, 0);
        }
    }
}
