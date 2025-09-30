using J_Tutors_Web_Platform.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace J_Tutors_Web_Platform.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // -------------------------------
        // PUBLIC VIEWS (no auth required)
        // -------------------------------

        // GET: /Home/Info
        // Renders: Views/Public/Info.cshtml
        public IActionResult Info()
        {
            // Using an explicit absolute path lets us keep any folder structure we want
            return View("~/Views/Public/Info.cshtml");
        }

        // GET: /Home/Login
        // Renders: Views/Public/Login.cshtml
        public IActionResult Login()
        {
            return View("~/Views/Public/Login.cshtml");
        }

        // GET: /Home/Register
        // Renders: Views/Public/Register.cshtml (placeholder until you create it)
        public IActionResult Register()
        {
            // TEMP: if the view doesn't exist yet this will 404; add the file when ready.
            return View("~/Views/Public/Register.cshtml");
        }

        // GET: /Home/Privacy
        // Renders: Views/Public/Privacy.cshtml
        public IActionResult Privacy()
        {
            return View("~/Views/Public/Privacy.cshtml");
        }

        // -------------------------------
        // USER VIEWS (post-login)
        // -------------------------------

        // GET: /Home/Dashboard  ? User landing page
        // Renders: Views/User/Dashboard.cshtml
        public IActionResult UDashboard()
        {
            return View("~/Views/User/UDashboard.cshtml");
        }

        // GET: /Home/Dashboard  ? User landing page
        // Renders: Views/User/Dashboard.cshtml
        public IActionResult UFileLibrary()
        {
            return View("~/Views/User/UFileLibrary.cshtml");
        }

        public IActionResult UProfile()
        {
            return View("~/Views/User/UProfile.cshtml");
        }

        public IActionResult UEvents()
        {
            return View("~/Views/User/UEvents.cshtml");
        }

        public IActionResult UEventHistory()
        {
            return View("~/Views/User/UEventHistory.cshtml");
        }
        public IActionResult UPointsLedger()
        {
            return View("~/Views/User/UPointsLedger.cshtml");
        }

        public IActionResult UPointsLeaderboard()
        {
            return View("~/Views/User/UPointsLeaderboard.cshtml");
        }

        public IActionResult UBooking()
        {
            return View("~/Views/User/UBooking.cshtml");
        }

        public IActionResult USessions()
        {
            return View("~/Views/User/USessions.cshtml");
        }


        // -------------------------------
        // ADMIN VIEWS (post-login)
        // -------------------------------

        // GET: /Home/AdminDashboard  ? Admin landing page
        // Renders: Views/Admin/Dashboard.cshtml (create later)
        public IActionResult ADashboard()
        {
            return View("~/Views/Admin/ADashboard.cshtml");
        }
        public IActionResult ASessionsCalendar()
        {
            return View("~/Views/Admin/ASessionsCalendar.cshtml");
        }
        public IActionResult AUserList()
        {
            return View("~/Views/Admin/AUserList.cshtml");
        }
        public IActionResult AUserDetails()
        {
            return View("~/Views/Admin/AUserDetails.cshtml");
        }
        public IActionResult AEventList()
        {
            return View("~/Views/Admin/AEventList.cshtml");
        }

        public IActionResult AEventDetails()
        {
            return View("~/Views/Admin/AEventDetails.cshtml");
        }
        public IActionResult AFiles()
        {
            return View("~/Views/Admin/AFiles.cshtml");
        }
        public IActionResult APricing()
        {
            return View("~/Views/Admin/APricing.cshtml");
        }
        public IActionResult ALeaderboard()
        {
            return View("~/Views/Admin/ALeaderboard.cshtml");
        }
        public IActionResult AAnalytics()
        {
            return View("~/Views/Admin/AAnalytics.cshtml");
        }
        public IActionResult AAccount()
        {
            return View("~/Views/Admin/AAccount.cshtml");
        }



        // -------------------------------
        // STANDARD ERROR ACTION
        // -------------------------------
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
