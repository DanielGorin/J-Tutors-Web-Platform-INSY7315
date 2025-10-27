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

        public IActionResult ASessionCalender()
        {
            var list = _adminService.GetTutoringSessions();

            return View(list);
        }
    }
}
