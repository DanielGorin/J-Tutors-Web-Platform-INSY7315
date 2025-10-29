#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.Authorization; // ← enable if needed
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using J_Tutors_Web_Platform.Models.Shared;

namespace J_Tutors_Web_Platform.Controllers
{
    /// <summary>
    /// CONTROLLER: AdminAgendaController
    /// PURPOSE: Admin "Agenda" page and its tabs:
    ///   - Shell: /AdminAgenda/Agenda
    ///   - Tabs:  _AgendaSlots, _AgendaInbox, _AgendaCalendar
    ///   - Actions: Accept / Deny / Cancel / MarkPaid
    /// 
    /// Views expected:
    ///   ~/Views/Admin/Agenda.cshtml               (shell with tabs)
    ///   ~/Views/Admin/_AgendaSlots.cshtml         (partial)
    ///   ~/Views/Admin/_AgendaInbox.cshtml         (partial)
    ///   ~/Views/Admin/_AgendaCalendar.cshtml      (partial)
    ///
    /// Services:
    ///   - AdminAgendaService (data fetch & mutations for agenda)
    /// </summary>
    // [Authorize(Roles = "Admin")] // optional hardening
    public class AdminAgendaController : Controller
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // Dependencies
        // ─────────────────────────────────────────────────────────────────────────────
        private readonly AdminAgendaService _agenda;

        public AdminAgendaController(AdminAgendaService agenda)
        {
            _agenda = agenda;
        }

        // ============================================================================
        // SHELL: Agenda (tab host)
        // ============================================================================

        /// <summary>
        /// GET: /AdminAgenda/Agenda
        /// Renders the tabbed shell with header/badges. Tabs load via partials.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Agenda()
        {
            ViewData["NavSection"] = "Admin";

            var counts = await _agenda.GetAgendaCountsAsync();

            var vm = new AdminAgendaShellVM
            {
                ScheduledCount = counts.scheduled,
                AcceptedCount = counts.accepted,
                PaidCount = counts.paid,
                CancelledCount = counts.cancelled
            };

            return View("~/Views/Admin/AAgenda.cshtml", vm);
        }

        // ============================================================================
        // TABS: Slots
        // ============================================================================

        /// <summary>
        /// GET partial: Availability blocks (optionally filtered by date range).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> _AgendaSlots(DateTime? from = null, DateTime? to = null, int? minutes = null, int? adminId = null)
        {
            ViewData["NavSection"] = "Admin";

            var blocks = await _agenda.GetAvailabilityBlocksAsync(from, to, adminId);

            var vm = new AgendaSlotsVM
            {
                From = from,
                To = to,
                Minutes = minutes,
                Blocks = blocks.ToList()
            };

            return PartialView("~/Views/Admin/_AgendaSlots.cshtml", vm);
        }

        /// <summary>
        /// POST: Create a new availability block for an admin.
        /// Expected inputs: adminId, date (yyyy-MM-dd), start (HH:mm), durationMinutes.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAvailability(int adminId, string date, string start, int durationMinutes)
        {
            try
            {
                if (!DateTime.TryParse(date, out var d))
                    throw new ArgumentException("Invalid date.");
                if (!TimeSpan.TryParseExact(start, "hh\\:mm", null, out var st))
                    throw new ArgumentException("Invalid start time (HH:mm).");

                await _agenda.CreateAvailabilityBlockAsync(adminId, d.Date, st, durationMinutes);
                TempData["AgendaOk"] = "Availability block created.";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = ex.Message;
            }

            // Return to the shell; the Slots tab can be reloaded client-side
            return RedirectToAction(nameof(Agenda));
        }

        /// <summary>
        /// POST: Delete an availability block by id.
        /// </summary>
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

        // ============================================================================
        // TABS: Inbox
        // ============================================================================

        /// <summary>
        /// GET partial: Inbox grouped by TutoringSessionStatus.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> _AgendaInbox()
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

            return PartialView("~/Views/Admin/_AgendaInbox.cshtml", vm);
        }

        // ============================================================================
        // TABS: Calendar
        // ============================================================================

        /// <summary>
        /// GET partial: Month grid calendar.
        /// Always shows Accepted; optionally toggles Scheduled as well.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> _AgendaCalendar(int? year = null, int? month = null, bool includeScheduled = false, int? adminId = null)
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

            return PartialView("~/Views/Admin/_AgendaCalendar.cshtml", vm);
        }

        // ============================================================================
        // ACTIONS: Session lifecycle (Accept / Deny / Cancel / MarkPaid)
        // ============================================================================

        /// <summary>POST: Accept a Scheduled session → Accepted.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptSession(int id)
        {
            try
            {
                var rows = await _agenda.AcceptSessionAsync(id);
                TempData[rows > 0 ? "AgendaOk" : "AgendaWarn"] = rows > 0 ? "Session accepted." : "No change (status mismatch?).";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = ex.Message;
            }
            return RedirectToAction(nameof(Agenda));
        }

        /// <summary>POST: Deny a Scheduled session → Denied.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DenySession(int id)
        {
            try
            {
                var rows = await _agenda.DenySessionAsync(id);
                TempData[rows > 0 ? "AgendaOk" : "AgendaWarn"] = rows > 0 ? "Session denied." : "No change (status mismatch?).";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = ex.Message;
            }
            return RedirectToAction(nameof(Agenda));
        }

        /// <summary>POST: Cancel a Scheduled/Accepted session → Cancelled (sets CancellationDate).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSession(int id)
        {
            try
            {
                var rows = await _agenda.CancelSessionAsync(id);
                TempData[rows > 0 ? "AgendaOk" : "AgendaWarn"] = rows > 0 ? "Session cancelled." : "No change (status mismatch?).";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = ex.Message;
            }
            return RedirectToAction(nameof(Agenda));
        }

        /// <summary>POST: Mark an Accepted/Completed session → Paid (sets PaidDate).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
            try
            {
                var rows = await _agenda.MarkSessionPaidAsync(id);
                TempData[rows > 0 ? "AgendaOk" : "AgendaWarn"] = rows > 0 ? "Session marked Paid." : "No change (status mismatch?).";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = ex.Message;
            }
            return RedirectToAction(nameof(Agenda));
        }
    }
}
