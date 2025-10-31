using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using J_Tutors_Web_Platform.ViewModels;


namespace J_Tutors_Web_Platform.Controllers
{
    /// <summary>
    /// CONTROLLER: UserController
    /// PURPOSE: Thin HTTP layer for user features. All DB work is in services.
    /// AREAS: Profile (view/update/theme), Leaderboard, Points Ledger, Bookings (quote/reserve)
    /// </summary>
    public class UserController : Controller
    {
        private readonly UserProfileService _profiles;
        private readonly UserLeaderboardService _leaderboard;
        private readonly UserLedgerService _ledger;
        private readonly UserBookingService _booking;
        private readonly ILogger<UserController> _log;

        public UserController(
            UserProfileService profiles,
            UserLeaderboardService leaderboard,
            UserLedgerService ledger,
            UserBookingService booking,
            ILogger<UserController> log)
        {
            _profiles = profiles;
            _leaderboard = leaderboard;
            _ledger = ledger;
            _booking = booking;
            _log = log;
        }

        // ============================================================================
        // PROFILE
        // ============================================================================

        [HttpGet]
        public async Task<IActionResult> UProfile()
        {
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Login", "Home");

            var vm = await _profiles.GetProfileAsync(username);
            if (vm is null) return NotFound();

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UProfile(
            [Bind("Username,Phone,Email,SubjectInterest,LeaderboardVisible,ThemePreference")]
            UserProfileViewModel form)
        {
            var currentUsername = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUsername))
                return RedirectToAction("Login", "Home");

            var who = await _profiles.GetUserIdAndUsernameAsync(currentUsername);
            if (who is null) return NotFound();

            int userId = who.Value.userId;
            string existingUsername = who.Value.existingUsername;

            // Only check uniqueness if username is changing
            if (!existingUsername.Equals(form.Username, StringComparison.OrdinalIgnoreCase))
            {
                if (await _profiles.IsUsernameTakenAsync(form.Username))
                {
                    ModelState.AddModelError(nameof(form.Username), "That username is already taken.");
                    ViewData["NavSection"] = "User";

                    // Rehydrate with DB values, then overlay attempted edits
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

            await _profiles.UpdateProfileAsync(userId, form);

            // Refresh auth cookie if username changed
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

            // Persist theme cookie (used by layout)
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

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetTheme(string theme)
        {
            var pref = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light"
                    : string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark"
                    : "";

            Response.Cookies.Append("ThemePreference", pref, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Secure = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });

            var username = User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(username))
                await _profiles.UpdateThemePreferenceAsync(username, pref);

            return Ok(new { ok = true, pref });
        }

        // ============================================================================
        // LEADERBOARD
        // ============================================================================

        [HttpGet]
        public async Task<IActionResult> UPointsLeaderboard(
            LeaderboardViewMode mode = LeaderboardViewMode.Current,
            LeaderboardTimeFilter time = LeaderboardTimeFilter.ThisMonth,
            string? search = null,
            int page = 1,
            int pageSize = 20)
        {
            var currentUsername = User?.Identity?.Name;
            var vm = await _leaderboard.GetPageAsync(currentUsername, mode, time, page, pageSize);

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UPointsLeaderboard.cshtml", vm);
        }

        // ============================================================================
        // POINTS LEDGER
        // ============================================================================
        [HttpGet]
        public async Task<IActionResult> UPointsLedger()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Home");

            var who = await _profiles.GetUserIdAndUsernameAsync(username);
            if (who is null) return NotFound();

            int userId = who.Value.userId;
            var rows = await _ledger.GetReceiptRowsAsync(userId);

            var vm = new UserLedgerPageViewModel { UserId = userId, Rows = rows };

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UPointsLedger.cshtml", vm);
        }



        // ---------------- BOOKING ----------------

        [HttpGet]
        public IActionResult UBooking()
        {
            var vm = new UserBookingViewModel
            {
                Subjects = _booking.GetSubjectsForBooking(),

            };

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UBooking.cshtml", vm);
        }

        [HttpGet]
        public IActionResult BookingSubjectConfig(int subjectId)
        {
            var cfg = _booking.GetSubjectConfig(subjectId);
            if (cfg == null) return NotFound();
            return Json(cfg);
        }

        [HttpGet]
        public async Task<IActionResult> BookingAvailability(int subjectId, int durationMinutes, int year, int month)
        {
            try
            {

                int? adminId = null;
                var vm = await _booking.GetAvailabilityMonthAsync(subjectId, durationMinutes, year, month, adminId);
                return Json(vm);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "BookingAvailability failed for subject {SubjectId} {Year}-{Month}", subjectId, year, month);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult BookingQuote(int subjectId, int durationMinutes, int discountPercent)
        {
            try
            {
                var q = _booking.CalculateQuote(subjectId, durationMinutes, discountPercent);
                return Json(q);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "BookingQuote failed for subject {SubjectId}", subjectId);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookingRequest([FromForm] BookingRequestVM dto)
        {
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Login", "Home");

            var who = await _profiles.GetUserIdAndUsernameAsync(username);
            if (who is null)
            {
                TempData["BookingError"] = "User not found.";
                return RedirectToAction(nameof(UBooking));
            }

            var userId = who.Value.userId;

            int? adminIdForSlotOwner = null;

            var res = await _booking.RequestBooking(userId, dto, adminIdForSlotOwner);

            if (!res.Ok)
            {
                TempData["BookingError"] = res.Message ?? "Could not create booking.";
                return RedirectToAction(nameof(UBooking));
            }

            TempData["BookingOk"] = res.Message ?? "Request sent.";
            return RedirectToAction(nameof(UBooking));
        }
    }
}
