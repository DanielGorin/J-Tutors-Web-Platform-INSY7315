using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace J_Tutors_Web_Platform.Controllers
{
    public class AdminController : Controller
    {
        private readonly AdminService _adminService;
        private readonly AuthService _authService;
        Models.Users.User user = new User();

        public AdminController(AdminService adminService, AuthService authService)
        {
            _adminService = adminService;
            _authService = authService;
        }

        //[HttpGet]
        public IActionResult ASessionCalender(DateTime BlockDate, TimeOnly StartTime, TimeOnly EndTime)
        {
            Console.WriteLine("Inside ASessionCalender GET method");

            var Username = User.Identity.Name;

            List<TutoringSession> tutoringSessions = _adminService.GetTutoringSessions();
            List<AvailabilityBlock> availabilitySlots = _adminService.GetAvailabilityBlocks();

            var calenderViewModel = new ViewModels.ASessionsCalenderViewModel
            {
                TutoringSessions = tutoringSessions,
                AvailabilityBlock = availabilitySlots
            };



            //=====================remove this after testing=============================
            var aavailabilitySlots = _adminService.GetAvailabilityBlocks();
            foreach (var slot in aavailabilitySlots)
            {
                Console.WriteLine($"ID: {slot.AvailabilityBlockID}, Date: {slot.BlockDate}, Start: {slot.StartTime}, End: {slot.EndTime}");
            }
            //============================================================================



            return View("ASessionsCalendar", calenderViewModel);
        }

        [HttpPost]
        public IActionResult CreateAvailabilitySlot(DateTime BlockDate, TimeOnly StartTime, int Duration)
        {
            var Username = User.Identity.Name;

            _adminService.CreateAvailabilitySlot(Username, BlockDate, StartTime, Duration);

            List<TutoringSession> tutoringSessions = _adminService.GetTutoringSessions();
            List<AvailabilityBlock> availabilitySlots = _adminService.GetAvailabilityBlocks();

            var calenderViewModel = new ViewModels.ASessionsCalenderViewModel
            {
                TutoringSessions = tutoringSessions,
                AvailabilityBlock = availabilitySlots
            };

            return View("ASessionsCalendar", calenderViewModel);
        }

        [HttpGet]
        public IActionResult AUserList()
        {
            var Username = User.Identity.Name;

            var userDirectoryViewModel = new ViewModels.UserDirectoryViewModel
            {
                UDR = _adminService.GetAllUsers(Username)
            };

            return View("~/Views/Admin/AUserList.cshtml", userDirectoryViewModel);
        }
        // ===== PRICING & SUBJECTS =====

        // GET: /Admin/APricing?subjectId=#
        [HttpGet]
        public IActionResult APricing(int? subjectId)
        {
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

        // POST: /Admin/CreateSubject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSubject(string subjectName)
        {
            if (!string.IsNullOrWhiteSpace(subjectName))
            {
                try { _adminService.CreateSubject(subjectName); }
                catch (Exception ex) { TempData["APricingError"] = ex.Message; }
            }
            return RedirectToAction(nameof(APricing));
        }

        // POST: /Admin/DeleteSubject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSubject(int subjectId)
        {
            try { _adminService.DeleteSubject(subjectId); }
            catch (Exception ex) { TempData["APricingError"] = ex.Message; }
            return RedirectToAction(nameof(APricing));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleSubject(int subjectId)
        {
            try { _adminService.ToggleSubjectActive(subjectId); }
            catch (Exception ex) { TempData["APricingError"] = ex.Message; }
            // Redirect WITHOUT subjectId so the right pane doesn’t open automatically
            return RedirectToAction(nameof(APricing));
        }


        // POST: /Admin/SavePricing
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePricing(int subjectId, string hourlyRate, string minHours, string maxHours, string maxPointDiscount)
        {
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

            // Business checks (keep the same rules as service)
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

    }
}
