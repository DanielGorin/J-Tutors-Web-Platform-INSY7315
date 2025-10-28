using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using J_Tutors_Web_Platform.Models.Users;

namespace J_Tutors_Web_Platform.Services
{
    // SERVICE: UserProfileService
    // PURPOSE: Encapsulate all DB reads/writes for the "My Profile" flow.
    // STORAGE: Azure SQL via ADO.NET (SqlConnection/SqlCommand).

    public class UserProfileService
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // CONFIG & LOGGING
        // ─────────────────────────────────────────────────────────────────────────────
        private readonly string _connStr;
        private readonly ILogger<UserProfileService> _log;

        public UserProfileService(IConfiguration cfg, ILogger<UserProfileService> log)
        {
            _connStr = cfg.GetConnectionString("AzureSql")!;
            _log = log;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // READ: Get user profile by Username
        // RETURNS: UserProfileViewModel (or null if not found)
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<UserProfileViewModel?> GetProfileAsync(string username)
        {
            var sql = @"SELECT TOP 1 * FROM Users WHERE Username = @u";
            // the above SQL Query pulls the relevent data for the current user
            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", username);

            await conn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new UserProfileViewModel
            {
                Username = r["Username"]?.ToString() ?? "",
                FirstName = r["FirstName"] as string,
                Surname = r["Surname"] as string,
                BirthDate = !r.IsDBNull(r.GetOrdinal("BirthDate"))
                    ? DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("BirthDate")))
                    : (DateOnly?)null,
                RegistrationDate = HasCol(r, "RegistrationDate") && !r.IsDBNull(r.GetOrdinal("RegistrationDate"))
                    ? DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("RegistrationDate")))
                    : (DateOnly?)null,
                Email = r["Email"] as string,
                Phone = r["Phone"] as string,
                SubjectInterest = r["SubjectInterest"] as string,
                LeaderboardVisible = !r.IsDBNull(r.GetOrdinal("LeaderboardVisible")) && Convert.ToBoolean(r["LeaderboardVisible"]),
                ThemePreference = r["ThemePreference"] as string
            };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // READ: Resolve (UserID, Username) for the currently signed-in Username
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<(int userId, string existingUsername)?> GetUserIdAndUsernameAsync(string currentUsername)
        {
            await using var conn = new SqlConnection(_connStr);
            await using var find = new SqlCommand(
                "SELECT TOP 1 UserID, Username FROM Users WHERE Username=@u", conn);
            find.Parameters.AddWithValue("@u", currentUsername);

            await conn.OpenAsync();
            await using var r = await find.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            return (r.GetInt32(0), r.GetString(1));
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // READ: Username taken?
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<bool> IsUsernameTakenAsync(string username)
        {
            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username=@u", conn);
            cmd.Parameters.AddWithValue("@u", username);

            await conn.OpenAsync();
            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // WRITE: Update the editable fields for a user by UserID
        // RETURNS: void (controller decides on cookie refresh etc)
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task UpdateProfileAsync(
            int userId,
            UserProfileViewModel form)
        {
            var updateSql = @"UPDATE Users SET Username = @nu, Email = @e, Phone = @p, SubjectInterest = @si, LeaderboardVisible = @lb, ThemePreference = @th WHERE UserID = @id";
            //the above SQL query changes the users data based on their inputs
            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(updateSql, conn);
            cmd.Parameters.AddWithValue("@nu", form.Username);
            cmd.Parameters.AddWithValue("@e", (object?)NullIfEmpty(form.Email) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@p", (object?)NullIfEmpty(form.Phone) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@si", (object?)NullIfEmpty(form.SubjectInterest) ?? DBNull.Value);
            cmd.Parameters.Add("@lb", SqlDbType.Bit).Value = form.LeaderboardVisible;
            cmd.Parameters.AddWithValue("@th", (object?)NullIfEmpty(form.ThemePreference) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", userId);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // WRITE: Persist ThemePreference for a signed-in user
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task UpdateThemePreferenceAsync(string username, string pref)
        {
            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(
                "UPDATE Users SET ThemePreference=@p WHERE Username=@u", conn);
            cmd.Parameters.AddWithValue("@p", (object)pref ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", username);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // UTILITIES (local)
        // ─────────────────────────────────────────────────────────────────────────────
        private static object? NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;

        private static bool HasCol(SqlDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
