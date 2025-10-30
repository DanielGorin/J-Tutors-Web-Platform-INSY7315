using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.Models.Points;
using J_Tutors_Web_Platform.Services;

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

            var user = await _profiles.GetUserIdAndUsernameAsync(username);
            if (user == null) return NotFound();

            var receipts = await _ledger.GetReceiptsForUserAsync(user.Value.userId);
            var totals = await _ledger.GetTotalsForUserAsync(user.Value.userId);

            ViewBag.TotalEarned = totals.earned;
            ViewBag.TotalDeducted = totals.deducted;
            ViewBag.CurrentBalance = totals.balance;

            ViewData["NavSection"] = "User";
            return View(receipts);
        }

        // ============================================================================
        // BOOKINGS (Quote & Reserve)
        // ============================================================================

        /// <summary>
        /// GET /User/UBooking — initial booking screen (optional pre-selected subject).
        /// Sends a fully-hydrated VM. Also prints a console-only DEBUG slot summary.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UBooking(int? subjectId)
        {
            var vm = await BuildEmptyBookingVm();

            if (subjectId.HasValue)
            {
                vm.SelectedSubjectID = subjectId;
                var s = vm.Subjects.FirstOrDefault(x => x.SubjectID == subjectId.Value);
                if (s is not null)
                {
                    vm.HourlyRate = s.HourlyRate;
                    vm.MinHours = s.MinHours;
                    vm.MaxHours = s.MaxHours;
                    vm.MaxPointDiscount = s.MaxPointDiscount;
                }
            }

            // =============================== DEBUG BLOCK (REMOVE LATER) ===============================
            // Goal: On UBooking open, print "<NumberOfSlots> <LengthMinutes>" for common lengths
            // Window: today .. +60 days; All admins (adminId=null). Sliding step: 15 minutes.
            try
            {
                var from = DateTime.Today;
                var to = from.AddDays(60);

                // Adjust this list as you like (minutes)
                var lengths = new[] { 30, 45, 60, 75, 90, 105, 120, 150, 180 };

                var counts = await _booking.GetGlobalSlotCountsAsync(from, to, lengths, adminId: null);

                Console.WriteLine("===== DEBUG SLOT SUMMARY (NumberOfSlots LengthMinutes) =====");
                foreach (var kv in counts.OrderByDescending(k => k.Value))
                {
                    Console.WriteLine($"{kv.Value} {kv.Key}");
                }
                Console.WriteLine("===== END DEBUG SLOT SUMMARY =====");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DEBUG] Slot summary failed: " + ex.Message);
            }
            // ============================ END DEBUG BLOCK (REMOVE LATER) ==============================

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UBooking.cshtml", vm);
        }

        /// <summary>
        /// POST /User/QuoteBooking — validate inputs, compute quote, optionally list available slots.
        /// Uses Request.Form["showSlots"] == "1" to decide whether to load slots.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuoteBooking(UserBookingViewModel form)
        {
            // Rehydrate subjects and resolve selected subject
            var subjects = (await _booking.GetActiveSubjectsAsync()).ToList();
            var subj = subjects.FirstOrDefault(s => s.SubjectID == form.SelectedSubjectID);

            if (subj is null)
            {
                TempData["BookingError"] = "Please choose a subject.";
                var vmEmpty = await BuildEmptyBookingVm();
                ViewData["NavSection"] = "User";
                return View("~/Views/User/UBooking.cshtml", vmEmpty);
            }

            // Clamp hours to subject min/max, then quote
            var hours = form.HoursPerSession;
            if (subj.MinHours > 0 && hours < subj.MinHours) hours = subj.MinHours;
            if (subj.MaxHours > 0 && hours > subj.MaxHours) hours = subj.MaxHours;

            QuoteResult quote;
            try
            {
                quote = _booking.Quote(
                    hours,
                    Math.Max(1, form.SessionCount),
                    subj.HourlyRate,
                    form.PointsPercent,
                    subj.MaxPointDiscount);
            }
            catch (Exception ex)
            {
                TempData["BookingError"] = ex.Message;
                var vmErr = await BuildEmptyBookingVm();
                vmErr.SelectedSubjectID = subj.SubjectID;
                ViewData["NavSection"] = "User";
                return View("~/Views/User/UBooking.cshtml", vmErr);
            }

            // Base VM (before slots)
            var vm = new UserBookingViewModel
            {
                Subjects = subjects,
                SelectedSubjectID = subj.SubjectID,
                HoursPerSession = quote.HoursPerSession,
                SessionCount = quote.SessionCount,
                PointsPercent = quote.PointsPercentApplied,
                HourlyRate = subj.HourlyRate,
                MinHours = subj.MinHours,
                MaxHours = subj.MaxHours,
                MaxPointDiscount = subj.MaxPointDiscount,
                Quote = quote,
                AvailableSlots = new List<SlotOption>()
            };

            // Decide whether to load slots
            var showSlots = string.Equals(Request.Form["showSlots"], "1", StringComparison.OrdinalIgnoreCase);
            if (showSlots)
            {
                var today = DateTime.Today;
                var adminId = await _booking.FindAdminWithAvailabilityAsync(today, today.AddDays(60), preferredAdminId: null);
                if (adminId is null)
                {
                    TempData["BookingWarn"] = "No admins have availability in the next 60 days.";
                }
                else
                {
                    var slots = await _booking.GetAvailableSlotsAsync(
                        adminId.Value,
                        quote.HoursPerSession,
                        fromInclusive: today,
                        toExclusive: today.AddDays(60));

                    vm.AvailableSlots = slots.ToList();
                }
            }

            ViewData["NavSection"] = "User";
            return View("~/Views/User/UBooking.cshtml", vm);
        }

        /// <summary>
        /// POST /User/ReserveBooking — create pending sessions and consume availability.
        /// Parses CSV of selected starts "yyyy-MM-dd|HH:mm,..." from the view.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReserveBooking(
            int subjectId,
            decimal hoursPerSession,
            decimal pointsPercent,
            string selectedStartsCsv)
        {
            // Auth → resolve user id
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Login", "Home");

            var who = await _profiles.GetUserIdAndUsernameAsync(username);
            if (who is null) return NotFound();
            var userId = who.Value.userId;

            // Resolve subject + authoritative quote
            var subjects = await _booking.GetActiveSubjectsAsync();
            var subj = subjects.FirstOrDefault(s => s.SubjectID == subjectId);
            if (subj is null)
            {
                TempData["BookingError"] = "Subject missing.";
                return RedirectToAction(nameof(UBooking));
            }

            var quote = _booking.Quote(
                hoursPerSession,
                1, // per-session cost
                subj.HourlyRate,
                pointsPercent,
                subj.MaxPointDiscount);

            // Parse "yyyy-MM-dd|HH:mm" (24-hour)
            var chosen = new List<(DateTime date, TimeSpan startTime)>();
            if (!string.IsNullOrWhiteSpace(selectedStartsCsv))
            {
                foreach (var part in selectedStartsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var pieces = part.Split('|');
                    if (pieces.Length != 2) continue;

                    if (DateTime.TryParseExact(pieces[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
                        TimeSpan.TryParseExact(pieces[1], "HH\\:mm", CultureInfo.InvariantCulture, out var start))
                    {
                        chosen.Add((date.Date, start));
                    }
                }
            }

            if (chosen.Count == 0)
            {
                TempData["BookingError"] = "Please choose at least one slot.";
                return RedirectToAction(nameof(UBooking), new { subjectId });
            }

            // Use same availability policy as quote (dynamic admin)
            var today = DateTime.Today;
            var adminId = await _booking.FindAdminWithAvailabilityAsync(today, today.AddDays(60), preferredAdminId: null);
            if (adminId is null)
            {
                TempData["BookingError"] = "No admin with availability could be found for the selected dates.";
                return RedirectToAction(nameof(UBooking), new { subjectId });
            }

            var created = await _booking.CreatePendingSessionsAsync(
                userId,
                adminId.Value,
                subjectId,
                quote.HoursPerSession,
                subj.HourlyRate,
                quote.PointsPercentApplied,
                chosen);

            if (created == 0)
                TempData["BookingError"] = "No sessions could be reserved (slots may have been taken).";
            else if (created < chosen.Count)
                TempData["BookingWarn"] = $"Only {created} of {chosen.Count} sessions reserved.";
            else
                TempData["BookingOk"] = $"Reserved {created} pending session(s).";

            return RedirectToAction(nameof(UBooking), new { subjectId });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Private helpers (VM shaping)
        // ─────────────────────────────────────────────────────────────────────────────

        private async Task<UserBookingViewModel> BuildEmptyBookingVm()
        {
            var subjects = (await _booking.GetActiveSubjectsAsync()).ToList();

            return new UserBookingViewModel
            {
                Subjects = subjects,
                SelectedSubjectID = null,
                HoursPerSession = 1.0m,
                SessionCount = 1,
                PointsPercent = 0,
                HourlyRate = null,
                MinHours = null,
                MaxHours = null,
                MaxPointDiscount = null,
                Quote = null,
                AvailableSlots = new List<SlotOption>()
            };
        }
    }
}
