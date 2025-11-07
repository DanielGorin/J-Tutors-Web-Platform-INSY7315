/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * PointsService
 * File Purpose:
 * This is a service that handles points related methods
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */
#nullable enable
using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace J_Tutors_Web_Platform.Services
{
    // =========================================================================================
    // POINTS SERVICE (ADO.NET STYLE, MINIMAL AND PREDICTABLE)
    // =========================================================================================
    //
    // Definitions:
    //  - Type:
    //      0 = Earned      (positive)
    //      1 = Spent       (negative)
    //      2 = Adjustment  (+ or −)
    //
    // TOTAL   = sum(Type IN 0,2)                             (includes negative adjustments)
    // CURRENT = TOTAL − sum(absolute value of Type=1 spent)
    //
    // Spent linkage to a tutoring session = Reference "TS-{sessionId}" (no SessionID column).
    //
    // =========================================================================================
    public sealed class PointsService
    {
        private readonly IConfiguration _config;
        private const string ConnName = "AzureSql";

        public PointsService(IConfiguration config)
        {
            _config = config;
        }

        // ==================== Connection helpers ====================
        private string GetConnectionString()
            => _config.GetConnectionString(ConnName)
               ?? throw new InvalidOperationException($"Connection string '{ConnName}' not found.");

        private async Task<SqlConnection> OpenAsync()
        {
            var con = new SqlConnection(GetConnectionString());
            await con.OpenAsync();
            return con;
        }

        private static SqlCommand Cmd(SqlConnection con, string sql, params (string, object?)[] ps)
        {
            var cmd = new SqlCommand(sql, con) { CommandType = CommandType.Text };
            foreach (var (name, value) in ps)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd;
        }

        // ==================== In-transaction helpers ====================
        public async Task<int> GetCurrentAsync(int userId, SqlConnection con, SqlTransaction tx)
        {
            const string sql = @"
SELECT
  COALESCE(SUM(CASE WHEN Type IN (0,2) THEN Amount ELSE 0 END), 0)  -- Total (Earned + Adjustments)
  - COALESCE(SUM(CASE WHEN Type = 1 THEN -Amount ELSE 0 END), 0)    -- minus absolute Spent
FROM dbo.PointsReceipt
WHERE UserID = @uid;";

            await using var cmd = new SqlCommand(sql, con, tx);
            cmd.Parameters.AddWithValue("@uid", userId);
            var o = await cmd.ExecuteScalarAsync();
            return (o == null || o is DBNull) ? 0 : Convert.ToInt32(o, CultureInfo.InvariantCulture);
        }

        public async Task<bool> ExistsByReferenceAsync(string reference, SqlConnection con, SqlTransaction tx)
        {
            const string sql = @"SELECT TOP 1 1 FROM dbo.PointsReceipt WHERE Reference = @ref;";
            await using var cmd = new SqlCommand(sql, con, tx);
            cmd.Parameters.AddWithValue("@ref", reference);
            var o = await cmd.ExecuteScalarAsync();
            return !(o is null || o is DBNull);
        }

        public async Task<int?> CreateSpentForSessionIdempotentAsync(
            int userId,
            int adminId,
            int sessionId,
            int amountPositive,                 // caller passes positive; we store negative
            SqlConnection con,
            SqlTransaction tx)
        {
            if (amountPositive <= 0) throw new ArgumentOutOfRangeException(nameof(amountPositive));

            var reference = $"TS-{sessionId}";

            // If it already exists, return existing id (idempotent)
            if (await ExistsByReferenceAsync(reference, con, tx))
            {
                const string getSql = @"SELECT TOP 1 PointsReceiptID FROM dbo.PointsReceipt WHERE Reference = @ref;";
                await using var getCmd = new SqlCommand(getSql, con, tx);
                getCmd.Parameters.AddWithValue("@ref", reference);
                var existing = await getCmd.ExecuteScalarAsync();
                return (existing == null || existing is DBNull) ? null : Convert.ToInt32(existing, CultureInfo.InvariantCulture);
            }

            // No SessionID column here — we link purely via Reference = "TS-{sessionId}"
            const string insertSql = @"
INSERT INTO dbo.PointsReceipt
(UserID, AdminID, ReceiptDate, Type, Amount, Reason, Reference, AffectsAllTime, Notes)
OUTPUT INSERTED.PointsReceiptID
VALUES
(@uid,  @aid,  SYSUTCDATETIME(), 1,   @negAmt, 'Session request', @ref, 1, NULL);";

            await using var cmd = new SqlCommand(insertSql, con, tx);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@aid", adminId);
            cmd.Parameters.AddWithValue("@negAmt", -Math.Abs(amountPositive)); // store negative
            cmd.Parameters.AddWithValue("@ref", reference);

            var obj = await cmd.ExecuteScalarAsync();
            if (obj == null || obj is DBNull) return null;
            return Convert.ToInt32(obj, CultureInfo.InvariantCulture);
        }

        // (Kept for completeness; also does NOT write SessionID)
        public async Task<int?> CreateSpentForSession(
            int userId,
            int adminId,
            int sessionId,
            int amount,
            SqlConnection? existingCon = null,
            SqlTransaction? tx = null)
        {
            var negative = amount > 0 ? -amount : amount;
            var reference = $"TS-{sessionId}";

            const string sql = @"
INSERT INTO dbo.PointsReceipt
(UserID, AdminID, ReceiptDate, Type, Amount, Reason, Reference, AffectsAllTime, Notes)
OUTPUT INSERTED.PointsReceiptID
VALUES
(@uid,  @aid,  SYSUTCDATETIME(), 1,   @amt,    'Session request', @ref, 1, NULL);";

            bool openedHere = false;
            SqlConnection con;

            if (existingCon is not null)
            {
                con = existingCon;
            }
            else
            {
                con = await OpenAsync();
                openedHere = true;
            }

            try
            {
                await using var cmd = new SqlCommand(sql, con, tx)
                {
                    CommandType = CommandType.Text
                };
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.Parameters.AddWithValue("@aid", adminId);
                cmd.Parameters.AddWithValue("@amt", negative);
                cmd.Parameters.AddWithValue("@ref", reference);

                var obj = await cmd.ExecuteScalarAsync();
                if (obj == null || obj is DBNull) return null;
                return Convert.ToInt32(obj, CultureInfo.InvariantCulture);
            }
            finally
            {
                if (openedHere)
                    await con.DisposeAsync();
            }
        }

        // ==================== Public balance queries ====================
        public async Task<int> GetTotal(int userId)
        {
            const string sql = @"SELECT COALESCE(SUM(CASE WHEN Type IN (0,2) THEN Amount ELSE 0 END), 0) FROM dbo.PointsReceipt WHERE UserID = @uid;";
            await using var con = await OpenAsync();
            await using var cmd = Cmd(con, sql, ("@uid", userId));
            var obj = await cmd.ExecuteScalarAsync();
            if (obj == null || obj is DBNull) return 0;
            return Convert.ToInt32(obj, CultureInfo.InvariantCulture);
        }

        public async Task<int> GetCurrent(int userId)
        {
            const string sql = @"
SELECT
  COALESCE(SUM(CASE WHEN Type IN (0,2) THEN Amount ELSE 0 END), 0) AS TotalPoints,
  COALESCE(SUM(CASE WHEN Type = 1 THEN -Amount ELSE 0 END), 0)     AS SpentPositive
FROM dbo.PointsReceipt
WHERE UserID = @uid;";

            await using var con = await OpenAsync();
            await using var cmd = Cmd(con, sql, ("@uid", userId));
            await using var r = await cmd.ExecuteReaderAsync();

            if (!await r.ReadAsync()) return 0;

            var total = r.IsDBNull(0) ? 0 : r.GetInt32(0);
            var spent = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            return total - spent;
        }

        // ==================== Delete by Reference (simple refund) ====================
        public async Task<int> DeleteByReference(string reference, SqlConnection? existingCon = null, SqlTransaction? tx = null)
        {
            const string sql = @"DELETE FROM dbo.PointsReceipt WHERE Reference = @ref;";

            bool openedHere = false;
            SqlConnection con;

            if (existingCon is not null)
            {
                con = existingCon;
            }
            else
            {
                con = await OpenAsync();
                openedHere = true;
            }

            try
            {
                await using var cmd = new SqlCommand(sql, con, tx)
                {
                    CommandType = CommandType.Text
                };
                cmd.Parameters.AddWithValue("@ref", reference);
                return await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (openedHere)
                    await con.DisposeAsync();
            }
        }

        public async Task<bool> ExistsByReference(string reference)
        {
            const string sql = @"SELECT TOP 1 1 FROM dbo.PointsReceipt WHERE Reference = @ref;";
            await using var con = await OpenAsync();
            await using var cmd = Cmd(con, sql, ("@ref", reference));
            var obj = await cmd.ExecuteScalarAsync();
            return obj != null && obj != DBNull.Value;
        }

        // ==================== Create Adjustment (+/-) ====================
        public async Task<int?> CreateAdjustment(
            int userId,
            int adminId,
            int amount,
            string? reason,
            string? reference,
            string? notes = null)
        {
            const string sql = @"
INSERT INTO dbo.PointsReceipt
    (UserID, AdminID, ReceiptDate, Type, Amount, Reason, Reference, Notes)
OUTPUT INSERTED.PointsReceiptID
VALUES
    (@uid,  @aid,  SYSUTCDATETIME(), 2,   @amt,   @reason, @ref,     @notes);";

            await using var con = await OpenAsync();
            await using var cmd = new SqlCommand(sql, con) { CommandType = CommandType.Text };

            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@aid", adminId);
            cmd.Parameters.AddWithValue("@amt", amount);
            cmd.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ref", (object?)reference ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);

            var obj = await cmd.ExecuteScalarAsync();
            if (obj == null || obj is DBNull) return null;
            return Convert.ToInt32(obj, CultureInfo.InvariantCulture);
        }
    }
}
