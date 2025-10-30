#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using static J_Tutors_Web_Platform.ViewModels.AAgendaPageVM;

namespace J_Tutors_Web_Platform.Controllers
{
    // [Authorize(Roles="Admin")]
    public class AdminAgendaController : Controller
    {
        private readonly AdminAgendaService _agenda;
        private readonly AdminService _adminService;

        public AdminAgendaController(AdminAgendaService agenda, AdminService adminService)
        {
            _agenda = agenda;
            _adminService = adminService;
        }

        /// <summary>
        /// GET /AdminAgenda/Agenda  (Slots-only for Part 1)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Agenda()
        {
            ViewData["NavSection"] = "Admin";

            var (scheduled, accepted, paid, cancelled) = await _agenda.GetAgendaCountsAsync();

            // only load Slots data for Part 1
            var blocks = await _agenda.GetAvailabilityBlocksAsync(null, null, null);

            var vm = new AAgendaPageVM
            {
                ScheduledCount = scheduled,
                AcceptedCount = accepted,
                PaidCount = paid,
                CancelledCount = cancelled,
                ActiveTab = "slots",
                Slots = new AgendaSlotsVM
                {
                    From = null,
                    To = null,
                    Minutes = null,
                    Blocks = blocks.ToList()
                }
            };

            return View("~/Views/Admin/AAgenda.cshtml", vm);
        }


        // POST: /AdminAgenda/CreateAvailability
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAvailability(DateTime date, TimeSpan start, int durationMinutes)
        {
            try
            {
                var username = User?.Identity?.Name ?? "";
                if (string.IsNullOrWhiteSpace(username))
                    throw new InvalidOperationException("You must be logged in as an admin.");

                // Look up adminId from username
                var adminId = _adminService.GetAdminID(username);

                await _agenda.CreateAvailabilityBlockAsync(adminId, date, start, durationMinutes);
                TempData["AgendaOk"] = "Availability block created.";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = ex.Message;
            }
            return RedirectToAction(nameof(Agenda));
        }

        // POST: /AdminAgenda/DeleteAvailability
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAvailability(int id)
        {
            try
            {
                await _agenda.DeleteAvailabilityBlockAsync(id);
                TempData["AgendaOk"] = "Availability block deleted.";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = ex.Message;
            }
            return RedirectToAction(nameof(Agenda));
        }

    }
}
