#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.Authorization; // ← enable if you gate admin with roles
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

using J_Tutors_Web_Platform.Models.Scheduling;

namespace J_Tutors_Web_Platform.Controllers
{
    /// <summary>
    /// CONTROLLER: AdminController (with LEGACY Sessions Calendar kept for now)
    /// PURPOSE: All *non-Agenda* admin features, plus the old Sessions Calendar endpoints.
    ///
    /// Current areas:
    ///   - Dashboard, Users, Pricing/Subjects
    ///   - (Placeholders) Events, Files, Leaderboard, Analytics, Account
    ///   - LEGACY: ASessionCalender + CreateAvailabilitySlot (to be removed later)
    ///
    /// NOTE: The new tabbed Agenda lives in AdminAgendaController.
    /// </summary>
    // [Authorize(Roles = "Admin")] // optional hardening
    public class AdminController : Controller
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // Dependencies
        // ─────────────────────────────────────────────────────────────────────────────
        private readonly AdminService _adminService;
        private readonly AuthService _authService;
        Models.Users.User user = new User();

        public AdminController(AdminService adminService, AuthService authService)
        {
            _adminService = adminService;
            _authService = authService;
        }

        // ============================================================================
        // NAV / SHELL: Basic admin landing
        // ============================================================================

        [HttpGet]
        public IActionResult ADashboard()
        {
            ViewData["NavSection"] = "Admin";
            // TODO: Populate dashboard metrics/widgets here if desired
            return View("~/Views/Admin/ADashboard.cshtml");
        }

        // ============================================================================
        // USERS: Directory
        // ============================================================================

        [HttpGet]
        public IActionResult AUserList()
        {
            ViewData["NavSection"] = "Admin";

            var username = User.Identity?.Name ?? string.Empty;

            var vm = new UserDirectoryViewModel
            {
                UDR = _adminService.GetAllUsers(username)
            };

            return View("~/Views/Admin/AUserList.cshtml", vm);
        }

        // ============================================================================
        // PRICING & SUBJECTS
        // ============================================================================

        /// <summary>
        /// GET: /Admin/APricing?subjectId=#
        /// Shows subjects on the left and pricing editor on the right (if selected).
        /// </summary>
        [HttpGet]
        public IActionResult APricing(int? subjectId)
        {
            ViewData["NavSection"] = "Admin";

            var vm = new APricingViewModel
            {
                Subjects = _adminService.GetAllSubjects(),
                SelectedSubjectID = subjectId
            };

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

        /// <summary>POST: Create a new subject.</summary>
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
                TempData["APricingError"] = ex.Message;
            }
            return RedirectToAction(nameof(APricing));
        }

        /// <summary>POST: Delete a subject (and its PricingRule if needed).</summary>
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

        /// <summary>POST: Toggle subject active flag.</summary>
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

            // Redirect WITHOUT subjectId so the right pane does not auto-open
            return RedirectToAction(nameof(APricing));
        }

        /// <summary>POST: Upsert pricing for a subject.</summary>
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

            if (subjectId <= 0)
            {
                TempData["APricingError"] = "Choose a subject first.";
                return RedirectToAction(nameof(APricing));
            }

            var inv = CultureInfo.InvariantCulture;
            if (!decimal.TryParse(hourlyRate, NumberStyles.Number, inv, out var hr) ||
                !decimal.TryParse(minHours, NumberStyles.Number, inv, out var minh) ||
                !decimal.TryParse(maxHours, NumberStyles.Number, inv, out var maxh) ||
                !decimal.TryParse(maxPointDiscount, NumberStyles.Number, inv, out var mpd))
            {
                TempData["APricingError"] = "One or more inputs are invalid numbers.";
                return RedirectToAction(nameof(APricing), new { subjectId });
            }

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

        // ============================================================================
        // LEGACY SESSIONS CALENDAR (KEEP FOR NOW — DELETE AFTER AGENDA IS FULLY LIVE)
        // ============================================================================

        /// <summary>
        /// LEGACY: GET /Admin/ASessionCalender
        /// Older calendar/sessions page you had before the new Agenda.
        /// Uses AdminService.GetTutoringSessions() and GetAvailabilityBlocks().
        /// </summary>
        [HttpGet]
        public IActionResult ASessionCalender(DateTime BlockDate, TimeOnly StartTime, TimeOnly EndTime)
        {
            ViewData["NavSection"] = "Admin";
            Console.WriteLine("LEGACY: Inside ASessionCalender GET method");

            // (Optional) current admin username
            var username = User.Identity?.Name ?? string.Empty;

            // Load sessions + availability (legacy service calls)
            var tutoringSessions = _adminService.GetTutoringSessions();
            var availabilitySlots = _adminService.GetAvailabilityBlocks();

            // Build the legacy VM you already used
            var calenderViewModel = new ViewModels.ASessionsCalenderViewModel
            {
                TutoringSessions = tutoringSessions,
                AvailabilityBlock = availabilitySlots
            };

            // ----- DEBUG: Keep this while legacy is in use; remove later -----
            foreach (var slot in availabilitySlots)
            {
                Console.WriteLine($"[LEGACY] Availability ID: {slot.AvailabilityBlockID}, Date: {slot.BlockDate:yyyy-MM-dd}, " +
                                  $"Start: {slot.StartTime}, End: {slot.EndTime}");
            }
            // -----------------------------------------------------------------

            return View("~/Views/Admin/ASessionsCalendar.cshtml", calenderViewModel);
        }

        /// <summary>
        /// LEGACY: POST /Admin/CreateAvailabilitySlot
        /// Insert a single availability block (date + start + duration minutes).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAvailabilitySlot(DateTime BlockDate, TimeOnly StartTime, int Duration)
        {
            ViewData["NavSection"] = "Admin";

            var username = User.Identity?.Name ?? string.Empty;

            // Service creates the block (legacy method)
            _adminService.CreateAvailabilitySlot(username, BlockDate, StartTime, Duration);

            // Re-load data for the legacy view
            var tutoringSessions = _adminService.GetTutoringSessions();
            var availabilitySlots = _adminService.GetAvailabilityBlocks();

            var calenderViewModel = new ViewModels.ASessionsCalenderViewModel
            {
                TutoringSessions = tutoringSessions,
                AvailabilityBlock = availabilitySlots
            };

            return View("~/Views/Admin/ASessionsCalendar.cshtml", calenderViewModel);
        }

        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string NewPassword, string ConfirmPassword)
        {
            var adminUsername = User.FindFirst(ClaimTypes.Name)?.Value;
            var result = _authService.AdminLogin(adminUsername, currentPassword);

            if (NewPassword != ConfirmPassword)
            {
                Console.WriteLine("password dont match");
                return View("AAccount");
            }
            
            if (result != "Login Successful")
            {
                Console.WriteLine("current password incorrect");
                return View("AAccount");
            }

            _authService.ChangeAdminPassword(adminUsername, NewPassword);

            Console.WriteLine("password changed to " + NewPassword);
            return View("AAccount");
        }

        // ============================================================================
        // PLACEHOLDERS for other Admin areas (views you already have)
        // ============================================================================

        [HttpGet] public IActionResult AEventList() { ViewData["NavSection"] = "Admin"; return View("~/Views/Admin/AEventList.cshtml"); }
        [HttpGet] public IActionResult AFiles() { ViewData["NavSection"] = "Admin"; return View("~/Views/Admin/AFiles.cshtml"); }
        [HttpGet] public IActionResult ALeaderboard() { ViewData["NavSection"] = "Admin"; return View("~/Views/Admin/ALeaderboard.cshtml"); }
        [HttpGet] public IActionResult AAnalytics() { ViewData["NavSection"] = "Admin"; return View("~/Views/Admin/AAnalytics.cshtml"); }
        [HttpGet] public IActionResult AAccount() { ViewData["NavSection"] = "Admin"; return View("~/Views/Admin/AAccount.cshtml"); }
    }
}
