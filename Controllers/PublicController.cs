﻿using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.Models.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

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
        
        //============================User=================================

        [HttpPost]
        public async Task<IActionResult> Login(string Username, string Password)
        {
            var result = _authService.Login(Username, Password);

            if (result == "Login Successful")
            {
                //adding a role for student as there is not role in the db. this can be worked around with a seperate login for admins. also login only goes through users table.
                var role = "Student";

                var claims = new List<Claim>
                {
                    //adding claims for username and role
                    new Claim(ClaimTypes.Name, Username),
                    new Claim(ClaimTypes.Role, role)
                };

                //creating claims identity
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                //signing in the user with the created claims identity
                await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity)
                );

                //testing that claims are working
                Console.WriteLine(User.FindFirst(ClaimTypes.Name)?.Value);
                Console.WriteLine(User.FindFirst(ClaimTypes.Role)?.Value);


                return RedirectToAction("UDashboard", "Home");
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

        //============================Admin=================================

        [HttpPost]
        public async Task<IActionResult> AdminLogin(string Username, string Password)
        {
            var result = _authService.AdminLogin(Username, Password);

            if (result == "Login Successful")
            {
                //adding a role for Admin as there is not role in the db. also login only goes through Admin table.
                var role = "Admin";

                var claims = new List<Claim>
                {
                    //adding claims for username and role
                    new Claim(ClaimTypes.Name, Username),
                    new Claim(ClaimTypes.Role, role),
                };

                //creating claims identity
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                //signing in the user with the created claims identity
                await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity)
                );

                //testing that claims are working
                Console.WriteLine(User.FindFirst(ClaimTypes.Name)?.Value);
                Console.WriteLine(User.FindFirst(ClaimTypes.Role)?.Value);

                return RedirectToAction("ADashboard", "Admin");
                //return View("~/Views/Admin/ASessionsCalendar.cshtml");
            }
            else
            {
                return View("~/Views/Public/AdminLogin.cshtml", result);
            }
        }
    }
}
