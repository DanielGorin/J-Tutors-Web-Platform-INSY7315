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

        public AdminUserDirectoryController(AdminUserDirectoryService service)
        {
            _service = service;
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

            // Render in the required view location
            return View("~/Views/Admin/AUserList.cshtml", vm);
        }
    }
}
