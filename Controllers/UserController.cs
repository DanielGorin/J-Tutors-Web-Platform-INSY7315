using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

using J_Tutors_Web_Platform.Models.Points;
using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.Services;

namespace J_Tutors_Web_Platform.Controllers
{
    // CONTROLLER: UserController
    // PURPOSE: Thin HTTP layer for user features. All DB work is in services.
    // AREAS: Profile (view/update/theme) + Leaderboard (list/view)

    public class UserController : Controller
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // CONFIG & LOGGING
        // ─────────────────────────────────────────────────────────────────────────────
        private readonly UserProfileService _profiles;
        private readonly UserLeaderboardService _leaderboard;
        private readonly UserLedgerService _ledger;
        private readonly ILogger<UserController> _log;

        public UserController(
            UserProfileService profiles,
            UserLeaderboardService leaderboard,
            UserLedgerService ledger,
            ILogger<UserController> log)
        {
            _profiles = profiles;
            _leaderboard = leaderboard;
            _ledger = ledger;
            _log = log;
        }

        [HttpGet]
        public async Task<IActionResult> UPointsLedger()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Account");

            // Resolve user ID (reuse existing method from profile service)
            var user = await _profiles.GetUserIdAndUsernameAsync(username);
            if (user == null) return NotFound();

            var receipts = await _ledger.GetReceiptsForUserAsync(user.Value.userId);
            var totals = await _ledger.GetTotalsForUserAsync(user.Value.userId);

            ViewBag.TotalEarned = totals.earned;
            ViewBag.TotalDeducted = totals.deducted;
            ViewBag.CurrentBalance = totals.balance;

            return View(receipts);
        }


        // ─────────────────────────────────────────────────────────────────────────────
        // VIEW: /User/UProfile (GET)
        // PURPOSE: Render "My Profile" page
        // HTTP: GET
        // FLOW: Auth check → service read → 404/redirect if missing → view
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> UProfile()
        {
            // SUB-SEGMENT auth
            // ------------------------------------------------------------------
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Login", "Home");

            // SUB-SEGMENT load model
            // ------------------------------------------------------------------
            var vm = await _profiles.GetProfileAsync(username);
            if (vm is null)
                return NotFound(); // user row missing

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UProfile.cshtml", vm);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // VIEW: /User/UProfile (POST)
        // PURPOSE: Persist edits from "My Profile" form
        // HTTP: POST
        // FLOW: Auth check → enforce username uniqueness → service update → refresh cookie (if needed) → theme cookie → redirect
        // SECURITY: [ValidateAntiForgeryToken]
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UProfile(
            [Bind("Username,Phone,Email,SubjectInterest,LeaderboardVisible,ThemePreference")]
            UserProfileViewModel form)
        {
            // SUB-SEGMENT auth
            // ------------------------------------------------------------------
            var currentUsername = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUsername))
                return RedirectToAction("Login", "Home");

            // SUB-SEGMENT resolve current user id + existing username
            // ------------------------------------------------------------------
            var who = await _profiles.GetUserIdAndUsernameAsync(currentUsername);
            if (who is null) return NotFound();
            int userId = who.Value.userId;
            string existingUsername = who.Value.existingUsername;

            // SUB-SEGMENT uniqueness check (only if changing)
            // ------------------------------------------------------------------
            if (!existingUsername.Equals(form.Username, StringComparison.OrdinalIgnoreCase))
            {
                if (await _profiles.IsUsernameTakenAsync(form.Username))
                {
                    ModelState.AddModelError(nameof(form.Username), "That username is already taken.");
                    ViewData["NavSection"] = "User";

                    // Re-hydrate from DB so non-edited fields are accurate, then overlay attempted edits
                    var reload = await _profiles.GetProfileAsync(currentUsername) ?? new UserProfileViewModel();
                    reload.Username = form.Username;
                    reload.Email = form.Email;
                    reload.Phone = form.Phone;
                    reload.SubjectInterest = form.SubjectInterest;
                    reload.LeaderboardVisible = form.LeaderboardVisible;
                    reload.ThemePreference = form.ThemePreference;

                    return View("~/Views/User/UProfile.cshtml", reload);
                }
            }

            // SUB-SEGMENT update via service
            // ------------------------------------------------------------------
            await _profiles.UpdateProfileAsync(userId, form);

            // SUB-SEGMENT refresh auth cookie if username changed
            // ------------------------------------------------------------------
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

            // SUB-SEGMENT persist theme cookie (used by layout)
            // ------------------------------------------------------------------
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

        // ─────────────────────────────────────────────────────────────────────────────
        // VIEW: /User/UPointsLeaderboard (GET)
        // PURPOSE: Searchable, filterable leaderboard
        // HTTP: GET
        // FLOW: Gather filters → service builds page VM → view
        // NOTE: Service handles visibility rules, ranking, paging, and all SQL.
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> UPointsLeaderboard(
            LeaderboardViewMode mode = LeaderboardViewMode.Current,
            LeaderboardTimeFilter time = LeaderboardTimeFilter.ThisMonth,
            string? search = null,
            int page = 1,
            int pageSize = 20)
        {
            // SUB-SEGMENT who-am-I (optional, for pinning/visibility exception)
            // ------------------------------------------------------------------
            var currentUsername = User?.Identity?.Name;

            // SUB-SEGMENT data from service
            // ------------------------------------------------------------------
            var vm = await _leaderboard.GetPageAsync(
                currentUsername,
                mode,
                time,
                page,
                pageSize);

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UPointsLeaderboard.cshtml", vm);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // API: /User/SetTheme (POST)
        // PURPOSE: Update theme preference cookie (+ DB if signed-in)
        // HTTP: POST
        // FLOW: Normalize value → set cookie → service persists (if logged in) → 200 JSON
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpPost]
        [IgnoreAntiforgeryToken] // keep simple; consider CSRF later
        public async Task<IActionResult> SetTheme(string theme)
        {
            // SUB-SEGMENT normalize input
            // ------------------------------------------------------------------
            var pref = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light"
                    : string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark"
                    : "";

            // SUB-SEGMENT write cookie (read by layout)
            // ------------------------------------------------------------------
            Response.Cookies.Append("ThemePreference", pref, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Secure = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });

            // SUB-SEGMENT persist to DB if logged in
            // ------------------------------------------------------------------
            var username = User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(username))
            {
                await _profiles.UpdateThemePreferenceAsync(username, pref);
            }

            return Ok(new { ok = true, pref });
        }

    }
}
