using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

namespace J_Tutors_Web_Platform.Controllers
{
    public class AdminController : Controller
    {
        private readonly AdminService _adminService;
        Models.Users.User user = new User();

        public AdminController(AdminService adminService)
        {
            _adminService = adminService;
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

        // POST: /Admin/ToggleSubject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleSubject(int subjectId, bool isActive)
        {
            try { _adminService.SetSubjectActive(subjectId, isActive); }
            catch (Exception ex) { TempData["APricingError"] = ex.Message; }
            return RedirectToAction(nameof(APricing), new { subjectId });
        }

        // POST: /Admin/SavePricing
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePricing(int subjectId, decimal hourlyRate, decimal minHours, decimal maxHours, decimal maxPointDiscount)
        {
            if (subjectId <= 0)
            {
                TempData["APricingError"] = "Choose a subject first.";
                return RedirectToAction(nameof(APricing));
            }

            var adminUsername = User.Identity?.Name ?? "";
            var adminId = _adminService.GetAdminID(adminUsername);

            try
            {
                _adminService.UpsertPricing(subjectId, adminId, hourlyRate, minHours, maxHours, maxPointDiscount);
                TempData["APricingOk"] = "Pricing saved.";
            }
            catch (Exception ex)
            {
                TempData["APricingError"] = ex.Message;
            }

            return RedirectToAction(nameof(APricing), new { subjectId });
        }

    }
}
