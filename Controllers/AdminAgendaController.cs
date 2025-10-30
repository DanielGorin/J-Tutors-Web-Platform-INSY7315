#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;

namespace J_Tutors_Web_Platform.Controllers
{
    public class AdminAgendaController : Controller
    {
        private readonly AdminAgendaService _agenda;

        public AdminAgendaController(AdminAgendaService agenda)
        {
            _agenda = agenda;
        }

        // SHELL — /AdminAgenda/Agenda
        [HttpGet]
        public async Task<IActionResult> Agenda()
        {
            ViewData["NavSection"] = "Admin";

            var (scheduled, accepted, paid, cancelled) = await _agenda.GetAgendaCountsAsync();

            var vm = new AAgendaPageVM
            {
                ScheduledCount = scheduled,
                AcceptedCount = accepted,
                PaidCount = paid,
                CancelledCount = cancelled,
                ActiveTab = "slots"
                // Slots stays null; the Slots tab loads via HTMX
            };

            return View("~/Views/Admin/AAgenda.cshtml", vm);
        }

        // TABS — SLOTS → Views/Admin/AAgendaSlots.cshtml
        [HttpGet]
        public async Task<IActionResult> Slots(DateTime? from = null, DateTime? to = null, int? minutes = null, int? adminId = null)
        {
            ViewData["NavSection"] = "Admin";

            var blocks = await _agenda.GetAvailabilityBlocksAsync(from, to, adminId);
            var vm = new AgendaSlotsVM
            {
                From = from,
                To = to,
                Minutes = minutes,
                Blocks = blocks // IReadOnlyList is fine
            };

            return PartialView("~/Views/Admin/AAgendaSlots.cshtml", vm);
        }

        // TABS — INBOX → Views/Admin/AAgendaInbox.cshtml
        [HttpGet]
        public async Task<IActionResult> Inbox()
        {
            ViewData["NavSection"] = "Admin";

            var (scheduled, accepted, paid, cancelled) = await _agenda.GetInboxBucketsAsync();
            var vm = new AgendaInboxVM
            {
                Scheduled = scheduled,
                Accepted = accepted,
                Paid = paid,
                Cancelled = cancelled
            };

            return PartialView("~/Views/Admin/AAgendaInbox.cshtml", vm);
        }

        // TABS — CALENDAR → Views/Admin/AAgendaCalendar.cshtml
        [HttpGet]
        public async Task<IActionResult> Calendar(int? year = null, int? month = null, bool includeScheduled = false, int? adminId = null)
        {
            ViewData["NavSection"] = "Admin";

            var today = DateTime.Today;
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

            return PartialView("~/Views/Admin/AAgendaCalendar.cshtml", vm);
        }
    }
}
