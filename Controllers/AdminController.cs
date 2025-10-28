using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.Services;
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

        public IActionResult ASessionCalender(DateTime BlockDate, TimeOnly StartTime, TimeOnly EndTime)
        {
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

            return View("~/Views/Admin/ASessionsCalendar.cshtml");
        }
    }
}
