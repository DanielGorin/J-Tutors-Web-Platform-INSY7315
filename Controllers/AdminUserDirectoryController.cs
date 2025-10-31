#nullable enable

// ============================================================================
// USING STATEMENTS
// Admin-only controller for listing users and viewing user details
// ============================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;

namespace J_Tutors_Web_Platform.Controllers
{
    // ============================================================================
    // CONTROLLER: AdminUserDirectoryController
    // PURPOSE:
    //   - Show a paged, filterable, sortable list of users (admin view)
    //   - Show details for a single user, including their points
    //
    // NOTES:
    //   - This is the *admin* version of the user directory
    //   - Uses AdminUserDirectoryService for data
    //   - Uses PointsService to enrich the details view with points info
    // ============================================================================
    [Authorize(Roles = "Admin")]
    public sealed class AdminUserDirectoryController : Controller
    {
        // ------------------------------------------------------------------------
        // DEPENDENCIES
        // _service → main user directory logic (search, paging, sorting)
        // _points  → existing system for calculating user points
        // ------------------------------------------------------------------------
        private readonly AdminUserDirectoryService _service;
        private readonly PointsService _points;

        // ------------------------------------------------------------------------
        // CONSTRUCTOR
        // DI for both services
        // ------------------------------------------------------------------------
        public AdminUserDirectoryController(
            AdminUserDirectoryService service,
            PointsService points)
        {
            _service = service;
            _points = points;
        }

        // ========================================================================
        // INDEX (LIST VIEW)
        // GET: /AdminUserDirectory/Index
        //
        // Allows:
        //   - search (by name/username depending on service implementation)
        //   - timeframe filter (ThisMonth, LastMonth, etc.)
        //   - sorting (column + direction)
        //   - paging (page, pageSize)
        //
        // Renders: ~/Views/Admin/AUserList.cshtml
        // ========================================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            AdminDirectoryTimeframe timeframe = AdminDirectoryTimeframe.ThisMonth,
            string sortColumn = "Username",
            string sortDirection = "ASC",
            int page = 1,
            int pageSize = 25)
        {
            // tell layout/nav that we're in admin area
            ViewData["NavSection"] = "Admin";

            // call the service to get a fully-populated page VM
            var vm = await _service.GetPageAsync(
                search,
                timeframe,
                sortColumn,
                sortDirection,
                page,
                pageSize
            );

            // show the admin user list view
            return View("~/Views/Admin/AUserList.cshtml", vm);
        }

        // ========================================================================
        // DETAILS (SINGLE USER)
        // GET: /AdminUserDirectory/Details/{id}
        //
        // Steps:
        //   1) load basic admin-facing user info
        //   2) load points (total + current) from existing system
        //   3) render details view
        //
        // If user not found → redirect back to list with an error
        // ========================================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            ViewData["NavSection"] = "Admin";

            // 1) Get the basic user profile (read-only) by UserID
            AdminUserDetailsViewModel? vm = await _service.GetUserBasicsAsync(id);
            if (vm is null)
            {
                // reuse existing TempData key from other admin screens
                TempData["AgendaError"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // 2) Enrich with points data from your existing points service
            //    (this makes the details page more useful for admins)
            vm.PointsTotal = await _points.GetTotal(id);
            vm.PointsCurrent = await _points.GetCurrent(id);

            // 3) Render the Admin details page
            //    NOTE: if your view filename is different, update the path here
            return View("~/Views/Admin/AUserDetails.cshtml", vm);
        }
    }
}
