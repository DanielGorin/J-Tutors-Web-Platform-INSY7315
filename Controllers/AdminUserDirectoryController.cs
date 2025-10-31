#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;

namespace J_Tutors_Web_Platform.Controllers
{
    [Authorize(Roles = "Admin")]
    public sealed class AdminUserDirectoryController : Controller
    {
        private readonly AdminUserDirectoryService _service;
        private readonly PointsService _points;

        public AdminUserDirectoryController(
            AdminUserDirectoryService service,
            PointsService points)   // <-- add this
        {
            _service = service;
            _points = points;       // <-- add this
        }

        // GET: /AdminUserDirectory/Index
        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            AdminDirectoryTimeframe timeframe = AdminDirectoryTimeframe.ThisMonth,
            string sortColumn = "Username",
            string sortDirection = "ASC",
            int page = 1,
            int pageSize = 25)
        {
            ViewData["NavSection"] = "Admin";

            var vm = await _service.GetPageAsync(
                search,
                timeframe,
                sortColumn,
                sortDirection,
                page,
                pageSize
            );

            return View("~/Views/Admin/AUserList.cshtml", vm);
        }

        // GET: /AdminUserDirectory/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            ViewData["NavSection"] = "Admin";

            // 1) Get the basic user profile (read-only) by UserID
            AdminUserDetailsViewModel? vm = await _service.GetUserBasicsAsync(id);
            if (vm is null)
            {
                TempData["AgendaError"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // 2) Get points snapshot using your existing PointsService
            vm.PointsTotal = await _points.GetTotal(id);
            vm.PointsCurrent = await _points.GetCurrent(id);

            // 3) Render the Admin details page (use your chosen filename)
            return View("~/Views/Admin/AUserDetails.cshtml", vm);
            // If your file is "AUserDetails.cshtml" instead, update the path above.
        }
    }
}
