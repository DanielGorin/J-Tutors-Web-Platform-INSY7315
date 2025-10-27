using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using J_Tutors_Web_Platform.Models.Users;
using Azure;

namespace J_Tutors_Web_Platform.Controllers
{

    public class UserController : Controller
    {
        //SEGMENT config and logging
        //-------------------------------------------------------------------------------------------
        private readonly string _connStr;
        private readonly ILogger<UserController> _log;

        public UserController(IConfiguration cfg, ILogger<UserController> log)
        {
            _connStr = cfg.GetConnectionString("AzureSql")!;
            _log = log;
        }
        //-------------------------------------------------------------------------------------------

        //SEGMENT GET /User/UProfile
        // loads profile: read-only + editable fields
        //-------------------------------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> UProfile()
        {
            //SUB-SEGMENT ensure signed in
            //---------------------------------------------------------------
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Login", "Home");
            //---------------------------------------------------------------

            //SUB-SEGMENT query user row
            //---------------------------------------------------------------
            var sql = @"
SELECT TOP 1
    UserID, Username, FirstName, Surname,
    BirthDate, RegistrationDate,
    Email, Phone, SubjectInterest,
    LeaderboardVisible, ThemePreference
FROM Users
WHERE Username = @u";

            UserProfileViewModel vm;

            await using (var conn = new SqlConnection(_connStr))
            await using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@u", username);
                await conn.OpenAsync();

                await using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return NotFound();

                // map to view model
                vm = new UserProfileViewModel
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
            //---------------------------------------------------------------

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UProfile.cshtml", vm);
        }
        //-------------------------------------------------------------------------------------------

        //SEGMENT POST /User/UProfile
        // saves editable fields; refresh cookie if username changed
        //-------------------------------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UProfile(
            [Bind("Username,Phone,Email,SubjectInterest,LeaderboardVisible,ThemePreference")]
            UserProfileViewModel form)
        {
            //SUB-SEGMENT ensure signed in
            //---------------------------------------------------------------
            var currentUsername = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUsername))
                return RedirectToAction("Login", "Home");
            //---------------------------------------------------------------

            //SUB-SEGMENT resolve user id + current username
            //---------------------------------------------------------------
            int userId;
            string existingUsername;
            await using (var conn = new SqlConnection(_connStr))
            await using (var find = new SqlCommand("SELECT TOP 1 UserID, Username FROM Users WHERE Username=@u", conn))
            {
                find.Parameters.AddWithValue("@u", currentUsername);
                await conn.OpenAsync();
                await using var r = await find.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return NotFound();
                userId = r.GetInt32(0);
                existingUsername = r.GetString(1);
            }
            //---------------------------------------------------------------

            //SUB-SEGMENT if username changed, ensure unique
            //---------------------------------------------------------------
            if (!existingUsername.Equals(form.Username, StringComparison.OrdinalIgnoreCase))
            {
                if (await IsUsernameTakenAsync(form.Username))
                {
                    ModelState.AddModelError(nameof(form.Username), "That username is already taken.");
                    ViewData["NavSection"] = "User";
                    var reload = await ReloadProfileAsync(currentUsername);
                    // preserve user edits except the conflicting username
                    reload.Username = form.Username;
                    reload.Email = form.Email;
                    reload.Phone = form.Phone;
                    reload.SubjectInterest = form.SubjectInterest;
                    reload.LeaderboardVisible = form.LeaderboardVisible;
                    reload.ThemePreference = form.ThemePreference;
                    return View("~/Views/User/UProfile.cshtml", reload);
                }
            }
            //---------------------------------------------------------------

            //SUB-SEGMENT update allowed cols
            //---------------------------------------------------------------
            var updateSql = @"
UPDATE Users
SET Username = @nu,
    Email = @e,
    Phone = @p,
    SubjectInterest = @si,
    LeaderboardVisible = @lb,
    ThemePreference = @th
WHERE UserID = @id";

            await using (var conn = new SqlConnection(_connStr))
            await using (var cmd = new SqlCommand(updateSql, conn))
            {
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
            //---------------------------------------------------------------

            //SUB-SEGMENT refresh cookie if username changed
            //---------------------------------------------------------------
            if (!existingUsername.Equals(form.Username, StringComparison.OrdinalIgnoreCase))
            {
                var role = User?.FindFirst(ClaimTypes.Role)?.Value ?? "Student";
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, form.Username),
                    new Claim(ClaimTypes.Role, role)
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity));
            }
            //---------------------------------------------------------------
            //SEGMENT persist theme cookie so _ViewStart can read it next request
            //-------------------------------------------------------------------------------------------
            var themeCookieVal = string.IsNullOrWhiteSpace(form.ThemePreference) ? "" : form.ThemePreference;
            Response.Cookies.Append("ThemePreference", themeCookieVal, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Secure = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });


            TempData["ProfileSaved"] = "Profile updated.";
            return RedirectToAction(nameof(UProfile));
        }
        //-------------------------------------------------------------------------------------------

        //SEGMENT helpers
        //-------------------------------------------------------------------------------------------
        private static object? NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;

        private async Task<bool> IsUsernameTakenAsync(string username)
        {
            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username=@u", conn);
            cmd.Parameters.AddWithValue("@u", username);
            await conn.OpenAsync();
            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        private async Task<UserProfileViewModel> ReloadProfileAsync(string username)
        {
            // small reuse of the SELECT for GET
            var sql = @"
SELECT TOP 1
    UserID, Username, FirstName, Surname,
    BirthDate, RegistrationDate,
    Email, Phone, SubjectInterest,
    LeaderboardVisible, ThemePreference
FROM Users
WHERE Username = @u";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", username);
            await conn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) throw new InvalidOperationException("User not found.");

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

        private static bool HasCol(SqlDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        //-------------------------------------------------------------------------------------------

        //SEGMENT lightweight API - set theme from anywhere (navbar toggle)
        //-------------------------------------------------------------------------------------------
        [HttpPost]
        [IgnoreAntiforgeryToken] // keep simple; you can add validation later
        public async Task<IActionResult> SetTheme(string theme)
        {
            // normalize input -> "", "Light", "Dark"
            var pref = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light"
                    : string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark"
                    : "";

            // write cookie for immediate effect on next request
            Response.Cookies.Append("ThemePreference", pref, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Secure = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });

            // if logged in, persist to DB too
            var username = User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(username))
            {
                await using var conn = new SqlConnection(_connStr);
                await using var cmd = new SqlCommand(
                    "UPDATE Users SET ThemePreference=@p WHERE Username=@u", conn);
                cmd.Parameters.AddWithValue("@p", (object)pref ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@u", username);
                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }

            return Ok(new { ok = true, pref });
        }


    }


}
