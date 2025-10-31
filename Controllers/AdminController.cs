#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using J_Tutors_Web_Platform.Models.Scheduling;


namespace J_Tutors_Web_Platform.Controllers
{
    // ============================================================================
    // CONTROLLER: AdminController
    // PURPOSE:
    //   - All the "normal" admin features (dashboard, pricing, subjects, pages)
    //   - PLUS: the older/legacy Sessions Calendar endpoints that existed
    //     before the new AdminAgendaController was created.
    //
    // IMPORTANT:
    //   - The new /AdminAgenda/... stuff lives in AdminAgendaController
    //   - This file should slowly lose the legacy calendar once everyone moves
    //     to the new agenda
    // ============================================================================
    // [Authorize(Roles = "Admin")] // ← turn this on when you want hard admin gating
    public class AdminController : Controller
    {
        // ------------------------------------------------------------------------
        // DEPENDENCIES
        // _adminService → main admin/business logic (subjects, pricing, sessions)
        // _authService  → for actions that need to check/change admin credentials
        // ------------------------------------------------------------------------
        private readonly AdminService _adminService;
        private readonly AuthService _authService;

        // ------------------------------------------------------------------------
        // CONSTRUCTOR
        // DI: controller needs the 2 services above
        // ------------------------------------------------------------------------
        public AdminController(AdminService adminService, AuthService authService)
        {
            _adminService = adminService;
            _authService = authService;
        }

        // ========================================================================
        // ======================== ADMIN DASHBOARD ===============================
        // GET: /Admin/ADashboard
        // Simple landing page for admins
        // ========================================================================

        [HttpGet]
        public IActionResult ADashboard()
        {
            ViewData["NavSection"] = "Admin";
            // TODO: Add dashboard cards, metrics, charts, etc.
            return View("~/Views/Admin/ADashboard.cshtml");
        }

        // ========================================================================
        // ======================= PRICING & SUBJECTS ============================
        // Area for managing Subjects AND their pricing rules
        // Includes:
        //   - listing subjects
        //   - creating/deleting/toggling subjects
        //   - saving pricing for a subject
        // ========================================================================

        /// <summary>
        /// GET: /Admin/APricing?subjectId=#
        /// Shows subjects on the left and pricing editor on the right (if selected).
        /// </summary>
        [HttpGet]
        public IActionResult APricing(int? subjectId)
        {
            ViewData["NavSection"] = "Admin";

            // build VM with list of subjects
            var vm = new APricingViewModel
            {
                Subjects = _adminService.GetAllSubjects(),
                SelectedSubjectID = subjectId
            };

            // if user picked a subject → load its pricing
            if (subjectId.HasValue)
            {
                var pr = _adminService.GetPricingForSubject(subjectId.Value);
                vm.Pricing = pr;
                vm.HourlyRate = pr?.HourlyRate;
                vm.MinHours = pr?.MinHours;
                vm.MaxHours = pr?.MaxHours;
                vm.MaxPointDiscount = pr?.MaxPointDiscount;
            }

            return View("~/Views/Admin/APricing.cshtml", vm);
        }

        // ------------------------------------------------------------------------
        // POST: create subject
        // ------------------------------------------------------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSubject(string subjectName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(subjectName))
                    _adminService.CreateSubject(subjectName);
            }
            catch (Exception ex)
            {
                // bubble error to UI
                TempData["APricingError"] = ex.Message;
            }

            // always go back to pricing
            return RedirectToAction(nameof(APricing));
        }

        // ------------------------------------------------------------------------
        // POST: delete subject
        // ------------------------------------------------------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSubject(int subjectId)
        {
            try
            {
                _adminService.DeleteSubject(subjectId);
            }
            catch (Exception ex)
            {
                TempData["APricingError"] = ex.Message;
            }

            return RedirectToAction(nameof(APricing));
        }

        // ------------------------------------------------------------------------
        // POST: toggle subject active/inactive
        // NOTE: we redirect WITHOUT the subjectId so the right pane does not open
        // again automatically
        // ------------------------------------------------------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleSubject(int subjectId)
        {
            try
            {
                _adminService.ToggleSubjectActive(subjectId);
            }
            catch (Exception ex)
            {
                TempData["APricingError"] = ex.Message;
            }

            return RedirectToAction(nameof(APricing));
        }

        // ------------------------------------------------------------------------
        // POST: save / upsert pricing
        // Steps:
        //   1. validate subject
        //   2. parse all numbers
        //   3. validate business rules
        //   4. call service to save
        // ------------------------------------------------------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePricing(
            int subjectId,
            string hourlyRate,
            string minHours,
            string maxHours,
            string maxPointDiscount)
        {
            ViewData["NavSection"] = "Admin";

            // must have a subject
            if (subjectId <= 0)
            {
                TempData["APricingError"] = "Choose a subject first.";
                return RedirectToAction(nameof(APricing));
            }

            // parse numbers (invariant culture to avoid comma/dot mixups)
            var inv = CultureInfo.InvariantCulture;
            if (!decimal.TryParse(hourlyRate, NumberStyles.Number, inv, out var hr) ||
                !decimal.TryParse(minHours, NumberStyles.Number, inv, out var minh) ||
                !decimal.TryParse(maxHours, NumberStyles.Number, inv, out var maxh) ||
                !decimal.TryParse(maxPointDiscount, NumberStyles.Number, inv, out var mpd))
            {
                TempData["APricingError"] = "One or more inputs are invalid numbers.";
                return RedirectToAction(nameof(APricing), new { subjectId });
            }

            // validate business rules
            if (minh <= 0 || maxh <= 0 || maxh < minh)
            {
                TempData["APricingError"] = "Min/Max hours are invalid.";
                return RedirectToAction(nameof(APricing), new { subjectId });
            }
            if (hr < 0)
            {
                TempData["APricingError"] = "Hourly rate must be ≥ 0.";
                return RedirectToAction(nameof(APricing), new { subjectId });
            }
            if (mpd < 0)
            {
                TempData["APricingError"] = "Max points discount must be ≥ 0.";
                return RedirectToAction(nameof(APricing), new { subjectId });
            }

            // Capture WHO is setting the price (will be our only admin at this time int he future multiple admins are possible)
            var adminUsername = User.Identity?.Name ?? "";
            var adminId = _adminService.GetAdminID(adminUsername);

            try
            {
                _adminService.UpsertPricing(subjectId, adminId, hr, minh, maxh, mpd);
                TempData["APricingOk"] = "Pricing saved.";
            }
            catch (Exception ex)
            {
                TempData["APricingError"] = ex.Message;
            }

            return RedirectToAction(nameof(APricing), new { subjectId });
        }

        // ========================================================================
        // ========================= LEGACY CALENDAR ==============================
        // KEEP FOR NOW
        // This is the older version of the admin sessions calendar.
        // It uses the older AdminService calls directly:
        //   - GetTutoringSessions()
        //   - GetAvailabilityBlocks()
        //
        // The new version (better structured, tabbed, calendar + inbox) lives in
        // AdminAgendaController.
        // ========================================================================

        /// <summary>
        /// LEGACY: GET /Admin/ASessionCalender
        /// Older calendar/sessions page.
        /// </summary>
        [HttpGet]
        public IActionResult ASessionCalender(DateTime BlockDate, TimeOnly StartTime, TimeOnly EndTime)
        {
            ViewData["NavSection"] = "Admin";
            Console.WriteLine("LEGACY: Inside ASessionCalender GET method");

            // current admin
            var username = User.Identity?.Name ?? string.Empty;

            // load data the old way
            var tutoringSessions = _adminService.GetTutoringSessions();
            var availabilitySlots = _adminService.GetAvailabilityBlocks();

            // build old VM
            var calenderViewModel = new ViewModels.ASessionsCalenderViewModel
            {
                TutoringSessions = tutoringSessions,
                AvailabilityBlock = availabilitySlots
            };

            // -----------------------------
            // DEBUG: leave for now
            // -----------------------------
            foreach (var slot in availabilitySlots)
            {
                Console.WriteLine(
                    $"[LEGACY] Availability ID: {slot.AvailabilityBlockID}, " +
                    $"Date: {slot.BlockDate:yyyy-MM-dd}, Start: {slot.StartTime}, End: {slot.EndTime}");
            }

            return View("~/Views/Admin/ASessionsCalendar.cshtml", calenderViewModel);
        }

        /// <summary>
        /// LEGACY: POST /Admin/CreateAvailabilitySlot
        /// Creates a slot via the legacy AdminService.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAvailabilitySlot(DateTime BlockDate, TimeOnly StartTime, int Duration)
        {
            ViewData["NavSection"] = "Admin";

            var username = User.Identity?.Name ?? string.Empty;

            // create via legacy service
            _adminService.CreateAvailabilitySlot(username, BlockDate, StartTime, Duration);

            // reload legacy data
            var tutoringSessions = _adminService.GetTutoringSessions();
            var availabilitySlots = _adminService.GetAvailabilityBlocks();

            var calenderViewModel = new ViewModels.ASessionsCalenderViewModel
            {
                TutoringSessions = tutoringSessions,
                AvailabilityBlock = availabilitySlots
            };

            return View("~/Views/Admin/ASessionsCalendar.cshtml", calenderViewModel);
        }

        // ========================================================================
        // ===================== ACCOUNT / PROFILE STUFF ==========================
        // Change password, theme preference, etc.
        // ========================================================================

        // ------------------------------------------------------------------------
        // POST: Change admin password
        // Steps:
        //   1. verify current password
        //   2. compare new + confirm
        //   3. if OK → change
        // ------------------------------------------------------------------------
        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string NewPassword, string ConfirmPassword)
        {
            var adminUsername = User.FindFirst(ClaimTypes.Name)?.Value;
            var result = _authService.AdminLogin(adminUsername, currentPassword);

            // check if new passwords match
            if (NewPassword != ConfirmPassword)
            {
                Console.WriteLine("password dont match");
                return View("AAccount");
            }

            // check if current is correct
            if (result != "Login Successful")
            {
                Console.WriteLine("current password incorrect");
                return View("AAccount");
            }

            // do change
            _authService.ChangeAdminPassword(adminUsername, NewPassword);

            Console.WriteLine("password changed to " + NewPassword);
            return View("AAccount");
        }

        // ------------------------------------------------------------------------
        // POST: SetTheme
        // Stores theme in cookie + persists to DB for this admin
        // ------------------------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> SetTheme(string theme)
        {
            // normalise theme to Light/Dark/""
            var pref = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light"
                    : string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark"
                    : "";

            // write to cookie
            Response.Cookies.Append("ThemePreference", pref, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Secure = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });

            // also persist to DB if we know the user
            var username = User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(username))
                await _adminService.ChangeTheme(username, pref);

            return Ok(new { ok = true, pref });
        }

        // ========================================================================
        // ========================== OTHER ADMIN VIEWS ===========================
        // These are the "static" admin pages you already have.
        // They all just set NavSection and return the view.
        // ========================================================================

        [HttpGet]
        public IActionResult AEventList()
        {
            ViewData["NavSection"] = "Admin";
            return View("~/Views/Admin/AEventList.cshtml");
        }

        [HttpGet]
        public IActionResult AFiles()
        {
            ViewData["NavSection"] = "Admin";
            return View("~/Views/Admin/AFiles.cshtml");
        }

        [HttpGet]
        public IActionResult ALeaderboard()
        {
            ViewData["NavSection"] = "Admin";
            return View("~/Views/Admin/ALeaderboard.cshtml");
        }

        [HttpGet]
        public IActionResult AAnalytics()
        {
            ViewData["NavSection"] = "Admin";
            return View("~/Views/Admin/AAnalytics.cshtml");
        }

        [HttpGet]
        public IActionResult AAccount()
        {
            ViewData["NavSection"] = "Admin";
            return View("~/Views/Admin/AAccount.cshtml");
        }
    }
}
