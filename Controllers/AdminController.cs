/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * AdminController
 * File Purpose:
 * This controller supports general admin features inclduing the dashboard, subjects and pricing and account.
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */

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
    // -------------------------
    // CONTROLLER: AdminController (dashboard, subjects & pricing, account/theme, legacy calendar)
    // -------------------------

    public class AdminController : Controller
    {

        // -------------------------
        // DEPENDENCIES
        // -------------------------
        private readonly AdminService _adminService;
        private readonly AuthService _authService;


        public AdminController(AdminService adminService, AuthService authService)
        {
            _adminService = adminService;
            _authService = authService;
        }


        [HttpGet]
        public IActionResult ADashboard()
        {
            ViewData["NavSection"] = "Admin";
            return View("~/Views/Admin/ADashboard.cshtml");
        }



        // -------------------------
        // GET APRicing subjects list with optional pricing editor
        // -------------------------
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

            // if user picked a subject load its pricing
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
        // -------------------------
        // POST: CreateSubject (add a subject)
        // -------------------------

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

            // always go back to pricing
            return RedirectToAction(nameof(APricing));
        }

        // -------------------------
        // Post DeleteSubject (removes a subject)
        // -------------------------

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

        // -------------------------
        // POST: ToggleSubject (activate/deactivate) this indicates whether the subject will be included in the dropdown for users to request it
        // -------------------------

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

        // -------------------------
        // POST: SavePricing
        // -------------------------

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

            // Capture which admin is setting the price (will be our only admin at this time int he future multiple admins are possible)
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

        // -------------------------
        // LEGACY CODE BELLOW
        // -------------------------
        //[HttpGet]
        //public IActionResult ASessionCalender(DateTime BlockDate, TimeOnly StartTime, TimeOnly EndTime)
        //{
        //    ViewData["NavSection"] = "Admin";
        //    Console.WriteLine("LEGACY: Inside ASessionCalender GET method");

        //    // current admin
        //    var username = User.Identity?.Name ?? string.Empty;

        //    // load data the old way
        //    var tutoringSessions = _adminService.GetTutoringSessions();
        //    var availabilitySlots = _adminService.GetAvailabilityBlocks();

        //    // build old VM
        //    var calenderViewModel = new ViewModels.ASessionsCalenderViewModel
        //    {
        //        TutoringSessions = tutoringSessions,
        //        AvailabilityBlock = availabilitySlots
        //    };

        //    // -----------------------------
        //    // DEBUG: leave for now
        //    // -----------------------------
        //    foreach (var slot in availabilitySlots)
        //    {
        //        Console.WriteLine(
        //            $"[LEGACY] Availability ID: {slot.AvailabilityBlockID}, " +
        //            $"Date: {slot.BlockDate:yyyy-MM-dd}, Start: {slot.StartTime}, End: {slot.EndTime}");
        //    }

        //    return View("~/Views/Admin/ASessionsCalendar.cshtml", calenderViewModel);
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        //public IActionResult CreateAvailabilitySlot(DateTime BlockDate, TimeOnly StartTime, int Duration)
        //{
        //    ViewData["NavSection"] = "Admin";

        //    var username = User.Identity?.Name ?? string.Empty;

        //    // create via legacy service
        //    _adminService.CreateAvailabilitySlot(username, BlockDate, StartTime, Duration);

        //    // reload legacy data
        //    var tutoringSessions = _adminService.GetTutoringSessions();
        //    var availabilitySlots = _adminService.GetAvailabilityBlocks();

        //    var calenderViewModel = new ViewModels.ASessionsCalenderViewModel
        //    {
        //        TutoringSessions = tutoringSessions,
        //        AvailabilityBlock = availabilitySlots
        //    };

        //    return View("~/Views/Admin/ASessionsCalendar.cshtml", calenderViewModel);
        //}
        // -------------------------
        // LEGACY CODE ABOVE
        // -------------------------



        // -------------------------
        // POST: ChangePasswor
        // -------------------------
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

            // change the password
            _authService.ChangeAdminPassword(adminUsername, NewPassword);

            Console.WriteLine("password changed to " + NewPassword);
            return View("AAccount");
        }

        // -------------------------
        // POST: SetTheme (changes between light and dark modes)
        // -------------------------
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

        // -------------------------
        // SECTIN: Other Static Admin Views (nav-only pages)
        // -------------------------

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
