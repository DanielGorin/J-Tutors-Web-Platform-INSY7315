/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * UserLedgerService
 * File Purpose:
 * This is a service that handles user ledger methods
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */
#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace J_Tutors_Web_Platform.Services
{
    /// Minimal data provider for the User Points Ledger (date, amount, type only)
    public class UserLedgerService
    {
        private readonly string _connStr;
        private readonly ILogger<UserLedgerService> _log;

        public UserLedgerService(IConfiguration cfg, ILogger<UserLedgerService> log)
        {
            _connStr = cfg.GetConnectionString("AzureSql")!;
            _log = log;
        }

        public async Task<List<UserLedgerRowViewModel>> GetReceiptRowsAsync(int userId)
        {
            var rows = new List<UserLedgerRowViewModel>();

            const string sql = @"
SELECT
  PointsReceiptID,
  ReceiptDate,
  Type,
  Amount
FROM dbo.PointsReceipt
WHERE UserID = @uid
ORDER BY ReceiptDate DESC;";

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.Default);
            while (await r.ReadAsync())
            {
                var typeVal = r.GetValue(r.GetOrdinal("Type")); // robust against int/string
                rows.Add(new UserLedgerRowViewModel
                {
                    PointsReceiptID = r.GetInt32(r.GetOrdinal("PointsReceiptID")),
                    ReceiptDateUtc = DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal("ReceiptDate")), DateTimeKind.Utc),
                    Kind = ParseKindFromDb(typeVal),
                    Amount = r.GetInt32(r.GetOrdinal("Amount"))
                });
            }

            return rows;
        }

        // Helper: maps DB "Type" (0/1/2 OR "0"/"1"/"2" OR "Earned"/"Spent"/"Adjustment") to enum
        private static LedgerRowKind ParseKindFromDb(object? dbVal)
        {
            if (dbVal is null || dbVal is DBNull) return LedgerRowKind.Adjustment;

            switch (dbVal)
            {
                case int i: return i switch { 0 => LedgerRowKind.Earned, 1 => LedgerRowKind.Spent, 2 => LedgerRowKind.Adjustment, _ => LedgerRowKind.Adjustment };
                case long l: return l switch { 0 => LedgerRowKind.Earned, 1 => LedgerRowKind.Spent, 2 => LedgerRowKind.Adjustment, _ => LedgerRowKind.Adjustment };
                case short s: return s switch { 0 => LedgerRowKind.Earned, 1 => LedgerRowKind.Spent, 2 => LedgerRowKind.Adjustment, _ => LedgerRowKind.Adjustment };
                case byte b: return b switch { 0 => LedgerRowKind.Earned, 1 => LedgerRowKind.Spent, 2 => LedgerRowKind.Adjustment, _ => LedgerRowKind.Adjustment };
                case string st:
                    {
                        var v = st.Trim();
                        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asInt))
                            return ParseKindFromDb(asInt);

                        return v.Equals("earned", StringComparison.OrdinalIgnoreCase) ? LedgerRowKind.Earned
                             : v.Equals("spent", StringComparison.OrdinalIgnoreCase) ? LedgerRowKind.Spent
                             : v.Equals("adjustment", StringComparison.OrdinalIgnoreCase) ? LedgerRowKind.Adjustment
                             : LedgerRowKind.Adjustment;
                    }
                default:
                    return LedgerRowKind.Adjustment;
            }
        }
    }
}
