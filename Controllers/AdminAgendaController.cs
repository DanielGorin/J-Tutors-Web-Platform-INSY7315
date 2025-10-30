#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;

namespace J_Tutors_Web_Platform.Controllers
{
    // If you use auth/roles elsewhere, feel free to add:
    // [Authorize(Roles = "Admin")]
    public sealed class AdminAgendaController : Controller
    {
        private readonly AdminAgendaService _agenda;

        public AdminAgendaController(AdminAgendaService agenda)
        {
            _agenda = agenda;
        }

        // --------------------------------------------------------------------
        // Landing page: shows counters + lets the view decide which tab to show
        // --------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Agenda(string? tab = null, int? adminId = null)
        {
            // Build counts from Inbox buckets so we don’t need separate queries
            var inbox = await _agenda.GetInboxAsync(adminId);

            var vm = new AAgendaPageVM
            {
                ActiveTab = string.IsNullOrWhiteSpace(tab) ? "Slots" : tab,
                ScheduledCount = inbox.Scheduled?.Count ?? 0,
                AcceptedCount = inbox.Accepted?.Count ?? 0,
                PaidCount = inbox.Paid?.Count ?? 0,
                CancelledCount = inbox.Cancelled?.Count ?? 0
            };

            return View("~/Views/Admin/AAgenda.cshtml", vm);
        }

        // -----------------------
        // SLOTS (availability tab)
        // -----------------------
        [HttpGet]
        public async Task<IActionResult> Slots(DateTime? from = null, DateTime? to = null, int? minutes = null, int? adminId = null)
        {
            var blocks = await _agenda.GetAvailabilityBlocksAsync(from, to, adminId);

            var vm = new AgendaSlotsVM
            {
                From = from,
                To = to,
                Minutes = minutes,
                Blocks = blocks
            };

            return View("~/Views/Admin/AAgendaSlots.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSlot(int adminId, DateTime date, TimeSpan start, int durationMinutes)
        {
            try
            {
                await _agenda.CreateAvailabilityBlockAsync(adminId, date, start, durationMinutes);
                TempData["AgendaOk"] = "Availability block created.";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = $"Could not create availability block: {ex.Message}";
            }

            return RedirectToAction(nameof(Slots));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlot(int id)
        {
            try
            {
                await _agenda.DeleteAvailabilityBlockAsync(id);
                TempData["AgendaOk"] = "Availability block removed.";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = $"Could not remove availability block: {ex.Message}";
            }

            return RedirectToAction(nameof(Slots));
        }

        // ----------------
        // INBOX (work tab)
        // ----------------
        [HttpGet]
        public async Task<IActionResult> Inbox(int? adminId = null)
        {
            var vm = await _agenda.GetInboxAsync(adminId);
            return View("~/Views/Admin/AAgendaInbox.cshtml", vm);
        }

        // ---------------------------
        // CALENDAR (monthly sessions)
        // ---------------------------
        [HttpGet]
        public async Task<IActionResult> Calendar(
            int? year = null,
            int? month = null,
            bool includeScheduled = false,
            int? adminId = null)
        {
            var today = DateTime.UtcNow;
            var y = year ?? today.Year;
            var m = month ?? today.Month;

            var sessions = await _agenda.GetSessionsForCalendarAsync(y, m, includeScheduled, adminId);

            var vm = new AgendaCalendarVM
            {
                Year = y,
                Month = m,
                IncludeScheduled = includeScheduled,
                Sessions = sessions
            };

            return View("~/Views/Admin/AAgendaCalendar.cshtml", vm);
        }
    }
}
