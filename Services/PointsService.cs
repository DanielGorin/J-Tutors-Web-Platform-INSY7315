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
    // What this service does (plain English):
    //  - Calculates user points balances (Total and Current).
    //  - Creates receipts when points are SPENT (e.g., booking a session).
    //  - Creates receipts for ADJUSTMENTS (+/-) when admins manually change points.
    //  - Deletes receipts by a shared Reference string (simple "undo" pattern).
    //
    // How balances are defined:
    //  - Each row in dbo.PointsReceipt represents a single change to a user's points.
    //  - Type:
    //      0 = Earned      (positive)   → gains
    //      1 = Spent       (negative)   → costs
    //      2 = Adjustment  (+ or −)     → manual tweak
    //
    //  - TOTAL   = sum of Earned + sum of Adjustments (including negative adjustments)
    //  - CURRENT = TOTAL − sum(Spent as positive)    ← Spent rows are stored negative; we flip sign when summing
    //
    // Key conventions used here:
    //  - Session spend receipts:
    //      Type=Spent (1), Amount stored as NEGATIVE, Reference = "TS-{sessionId}"
    //      This makes it easy to delete (refund) later using the same Reference.
    //
    //  - Refund pattern:
    //      Refunds are implemented by DELETING the original Spent receipt via Reference.
    //      (This is intentionally simple per project goals. If you later need audit trails,
    //       switch deletion to marking as "voided" and update your queries accordingly.)
    //
    // Important design choice:
    //  - This service uses "ADO.NET style" (SqlConnection, SqlCommand) to match your project.
    //    No EF; no complicated layers. Keep it predictable and non-intrusive.
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

        // ==============================================
        // LOW-LEVEL: CONNECTION + COMMAND HELPERS
        // ==============================================
        //
        // These helpers are kept very small. The goal is to make each public method below
        // read like a clear sequence of SQL operations without a lot of framework magic.
        //
        // ----------------------------------------------

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

        // ===========================================================
        // PUBLIC API: BALANCE QUERIES
        // ===========================================================
        //
        // These methods compute the numbers you show to users/admins.
        // Queries are intentionally simple and push the logic to SQL.
        //
        // -----------------------------------------------------------

        /// <summary>
        /// TOTAL points for a user (does NOT subtract Spent).
        /// TOTAL = Earned + Adjustments(±)
        ///
        /// Why:
        ///  - Think of this as the user's "lifetime" points, optionally excluding some
        ///    adjustments if you later decide to mark them as non-all-time.
        /// </summary>
        public async Task<int> GetTotal(int userId)
        {
            const string sql = @"SELECT COALESCE(SUM(CASE WHEN Type IN (0,2) THEN Amount ELSE 0 END), 0) FROM dbo.PointsReceipt WHERE UserID = @uid;";

            await using var con = await OpenAsync();
            await using var cmd = Cmd(con, sql, ("@uid", userId));
            var obj = await cmd.ExecuteScalarAsync();
            if (obj == null || obj is DBNull) return 0;
            return Convert.ToInt32(obj, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// CURRENT points for a user (what they can spend right now).
        /// CURRENT = TOTAL − Spent
        ///
        /// Detail:
        ///  - Spent rows are stored as negative amounts (e.g., -15).
        ///  - When summing Spent, we flip the sign to positive via (-Amount).
        ///  - So "SpentPositive" below is the absolute cost accumulated.
        /// </summary>
        public async Task<int> GetCurrent(int userId)
        {
            const string sql = @"SELECT COALESCE(SUM(CASE WHEN Type IN (0,2) THEN Amount ELSE 0 END), 0) AS TotalPoints, COALESCE(SUM(CASE WHEN Type = 1 THEN -Amount ELSE 0 END), 0)      AS SpentPositive FROM dbo.PointsReceipt WHERE UserID = @uid;";

            await using var con = await OpenAsync();
            await using var cmd = Cmd(con, sql, ("@uid", userId));
            await using var r = await cmd.ExecuteReaderAsync();

            if (!await r.ReadAsync()) return 0;

            var total = r.IsDBNull(0) ? 0 : r.GetInt32(0);
            var spent = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            return total - spent;
        }

        // =====================================================================
        // PUBLIC API: CREATE A "SPENT" RECEIPT FOR A TUTORING SESSION
        // =====================================================================
        //
        // When a user requests a booking, we "charge" points immediately if they
        // have enough current points. This is stored as a Spent receipt:
        //
        //  - Type = Spent (1)
        //  - Amount is NEGATIVE (e.g., -10)
        //  - SessionID is populated (link back to the TutoringSession)
        //  - Reference = "TS-{sessionId}" (so we can easily delete to refund)
        //
        // This method supports optional existing SqlConnection/SqlTransaction so
        // the caller can make the receipt creation part of a larger transaction
        // (e.g., the same tx that inserts TutoringSession).
        //
        // ---------------------------------------------------------------------

        /// <summary>
        /// Creates a Spent receipt linked to a tutoring session.
        /// - Amount will be enforced negative on insert.
        /// - Reference pattern: "TS-{sessionId}".
        /// - AffectsAllTime is set to 1 by default (typical for spend).
        /// - Returns the new PointsReceiptID, or null if insert fails.
        ///
        /// Optional:
        ///  - Pass an open SqlConnection and SqlTransaction to do this inside a larger tx.
        /// </summary>
        public async Task<int?> CreateSpentForSession(
            int userId,
            int adminId,
            int sessionId,
            int amount,
            SqlConnection? existingCon = null,
            SqlTransaction? tx = null)
        {
            // Ensure the stored amount is negative (spent).
            var negative = amount > 0 ? -amount : amount;
            var reference = $"TS-{sessionId}";

            const string sql = @" INSERT INTO dbo.PointsReceipt (UserID, EventParticipationID, SessionID, AdminID, ReceiptDate, Type, Amount, Reason, Reference, AffectsAllTime, Notes) OUTPUT INSERTED.PointsReceiptID VALUES (@uid, NULL, @sid, @aid, SYSUTCDATETIME(), @type, @amt, @reason, @ref, 1, NULL);";

            bool openedHere = false;
            SqlConnection con;

            // If the caller supplied an existing connection (inside a tx), use it.
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
                cmd.Parameters.AddWithValue("@sid", sessionId);
                cmd.Parameters.AddWithValue("@aid", adminId);
                cmd.Parameters.AddWithValue("@type", 1); // Spent
                cmd.Parameters.AddWithValue("@amt", negative);
                cmd.Parameters.AddWithValue("@reason", "Session request");
                cmd.Parameters.AddWithValue("@ref", reference);

                var obj = await cmd.ExecuteScalarAsync();
                if (obj == null || obj is DBNull) return null;
                return Convert.ToInt32(obj, CultureInfo.InvariantCulture);
            }
            finally
            {
                // If we opened the connection in this method, we dispose it here.
                if (openedHere)
                    await con.DisposeAsync();
            }
        }

        // ==================================================
        // PUBLIC API: DELETE BY REFERENCE (SIMPLE "UNDO")
        // ==================================================
        //
        // This is the "refund" mechanism for sessions (and can be used for events).
        //
        //  - If an admin cancels or denies a session, we remove the associated Spent
        //    receipt using its Reference ("TS-{sessionId}").
        //
        //  - For events, you can use a consistent "EV-{eventId}" reference when
        //    creating Earned receipts, then delete by that same reference to undo.
        //
        // --------------------------------------------------

        /// <summary>
        /// Delete ALL receipts that match a given Reference string.
        /// Returns the number of rows deleted.
        /// </summary>
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

        // ===========================================
        // PUBLIC API: QUICK REFERENCE EXISTENCE CHECK
        // ===========================================
        //
        // Use this to guard against duplicate inserts in rare cases (e.g., double-submit).
        //
        // -------------------------------------------

        /// <summary>
        /// Returns true if at least one receipt exists with the given Reference.
        /// Useful for de-duping before insert.
        /// </summary>
        public async Task<bool> ExistsByReference(string reference)
        {
            const string sql = @"SELECT TOP 1 1 FROM dbo.PointsReceipt WHERE Reference = @ref;";

            await using var con = await OpenAsync();
            await using var cmd = Cmd(con, sql, ("@ref", reference));
            var obj = await cmd.ExecuteScalarAsync();
            return obj != null && obj != DBNull.Value;
        }

        // ==============================================================
        // PUBLIC API: CREATE AN ADJUSTMENT (POSITIVE OR NEGATIVE)
        // ==============================================================
        //
        // Admins can manually add or remove points for a user.
        //  - Type = Adjustment (2)
        //  - Amount can be + or −
        //  - Reason/Reference/Notes are optional context strings
        //
        // Tip:
        //  - For bulk event awards, it’s usually better to create Earned (Type=0) receipts
        //    in your event service with a shared "EV-{eventId}" Reference.
        //
        // --------------------------------------------------------------

        /// <summary>
        /// Create a single Adjustment receipt.
        /// Returns the new PointsReceiptID, or null if insert fails.
        /// </summary>
        public async Task<int?> CreateAdjustment(
            int userId,
            int adminId,
            int amount,
            string? reason,
            string? reference,
            bool affectsAllTime = true,
            string? notes = null)
        {
            const string sql = @"INSERT INTO dbo.PointsReceipt (UserID, EventParticipationID, SessionID, AdminID, ReceiptDate, Type, Amount, Reason, Reference, AffectsAllTime, Notes) OUTPUT INSERTED.PointsReceiptID VALUES (@uid, NULL, NULL, @aid, SYSUTCDATETIME(), @type, @amt, @reason, @ref, @all, @notes);";

            await using var con = await OpenAsync();
            await using var cmd = new SqlCommand(sql, con)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@aid", adminId);
            cmd.Parameters.AddWithValue("@type", 2); // Adjustment
            cmd.Parameters.AddWithValue("@amt", amount);
            cmd.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ref", (object?)reference ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@all", affectsAllTime ? 1 : 0);
            cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);

            var obj = await cmd.ExecuteScalarAsync();
            if (obj == null || obj is DBNull) return null;
            return Convert.ToInt32(obj, CultureInfo.InvariantCulture);
        }
    }
}
