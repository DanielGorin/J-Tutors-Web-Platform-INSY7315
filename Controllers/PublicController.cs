using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.Models.Users;
using Microsoft.AspNetCore.Mvc;

namespace J_Tutors_Web_Platform.Controllers
{
    public class PublicController : Controller
    {
        private readonly AuthService _authService;
        Models.Users.User user = new User();

        public PublicController(AuthService authService) 
        {
            _authService = authService;
        }

        [HttpPost]
        public IActionResult Login(string Username, string Password)
        {
            var result = _authService.Login(Username, Password);

            if (result == "Login Successful")
            {
                return View("~/Views/Home/Index.cshtml");
            }
            else
            {
                return View("~/Views/Public/Login.cshtml", result);
            }
        }

        [HttpPost]
        public IActionResult Register(string Email, string Username, string Password, string ConfirmPassword, string Phone, DateOnly BirthDate, string ThemePreference, bool LeaderboardVisible, string SubjectInterest, string FirstName, string Surname)
        {
            var result = _authService.Register(Email, Username, Password, ConfirmPassword, Phone, BirthDate, ThemePreference, SubjectInterest, FirstName, Surname);

            if (result == "Successfully created account")
            {
                return View("~/Views/Public/Login.cshtml");
            }
            else
            {
                return View("~/Views/Public/Register.cshtml", result);
            }
        }
    }
}
