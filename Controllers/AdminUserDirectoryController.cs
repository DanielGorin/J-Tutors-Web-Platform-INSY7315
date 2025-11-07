/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * AdminUserDirectoryController
 * File Purpose:
 * This is the controller for the User Directory a searchable sortable list of users the admin can access. this controller is used to search sort and use pagination for ht list of user details
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */

#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;

namespace J_Tutors_Web_Platform.Controllers
{
    // -------------------------
    // CONTROLLER: AdminUserDirectoryController (Lists users, shows details)
    // -------------------------
    [Authorize(Roles = "Admin")]
    public sealed class AdminUserDirectoryController : Controller
    {
        // -------------------------
        // DEPENDENCIES (directory and points) Links to the needed services
        // -------------------------
        private readonly AdminUserDirectoryService _service;
        private readonly PointsService _points;

        // -------------------------
        // CTOR
        // -------------------------
        public AdminUserDirectoryController(
            AdminUserDirectoryService service,
            PointsService points)
        {
            _service = service;
            _points = points;
        }

        // -------------------------
        // GET: Index (A paged directory with search and sort)
        // -------------------------
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

        // -------------------------
        // GET Details (shows the selected user in detail and allows the admin to adjust their points both positive and negative)
        // -------------------------
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            ViewData["NavSection"] = "Admin";

            AdminUserDetailsViewModel? vm = await _service.GetUserBasicsAsync(id);
            if (vm is null)
            {
                TempData["AgendaError"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            vm.PointsTotal = await _points.GetTotal(id);
            vm.PointsCurrent = await _points.GetCurrent(id);

            return View("~/Views/Admin/AUserDetails.cshtml", vm);
        }
    }
}
