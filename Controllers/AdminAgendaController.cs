#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;

namespace J_Tutors_Web_Platform.Controllers
{
    // [Authorize(Roles = "Admin")] // optional
    public sealed class AdminAgendaController : Controller
    {
        private readonly AdminAgendaService _agenda;

        public AdminAgendaController(AdminAgendaService agenda)
        {
            _agenda = agenda;
        }

        // --------------------------------------------------------------------
        // Landing page: preload counts + real Slots data so the tab shows items
        // --------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Agenda(string? tab = null, int? adminId = null)
        {
            var aid = adminId ?? 1; // TODO: replace 1 with your real AdminID lookup
            ViewBag.AdminId = aid;
            var inbox = await _agenda.GetInboxAsync(adminId);

            // simple default window for slots: today .. +30 days
            var from = DateTime.UtcNow.Date;
            var to = from.AddDays(30);

            var vm = new AAgendaPageVM
            {
                ActiveTab = string.IsNullOrWhiteSpace(tab) ? "Slots" : tab,
                ScheduledCount = inbox.Scheduled?.Count ?? 0,
                AcceptedCount = inbox.Accepted?.Count ?? 0,
                PaidCount = inbox.Paid?.Count ?? 0,
                CancelledCount = inbox.Cancelled?.Count ?? 0,
                Inbox = inbox,
                Slots = new AgendaSlotsVM
                {
                    From = from,
                    To = to,
                    Blocks = await _agenda.GetAvailabilityBlocksAsync(from, to, adminId)
                },
                Calendar = new AgendaCalendarVM
                {
                    Year = DateTime.UtcNow.Year,
                    Month = DateTime.UtcNow.Month,
                    IncludeScheduled = false,
                    Sessions = await _agenda.GetSessionsForCalendarAsync(
                        DateTime.UtcNow.Year,
                        DateTime.UtcNow.Month,
                        includeScheduled: false,
                        adminId: adminId)
                }
            };

            return View("~/Views/Admin/AAgenda.cshtml", vm);
        }

        // -----------------------
        // SLOTS (availability tab)
        // -----------------------
        [HttpGet]
        public async Task<IActionResult> Slots(DateTime? from = null, DateTime? to = null, int? minutes = null, int? adminId = null)
        {
            var f = from ?? DateTime.UtcNow.Date;
            var t = to ?? f.AddDays(30);

            var blocks = await _agenda.GetAvailabilityBlocksAsync(f, t, adminId);

            var vm = new AgendaSlotsVM
            {
                From = f,
                To = t,
                Minutes = minutes,
                Blocks = blocks
            };

            return View("~/Views/Admin/AAgendaSlots.cshtml", vm);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSlot(int? adminId, DateTime date, string start, int durationMinutes)
        {
            // Ensure we have a valid admin id (prevents FK crashes)
            if (adminId is null || adminId <= 0)
            {
                TempData["AgendaError"] = "Missing or invalid Admin ID.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            // Business rule: no past / no within 3 days
            var earliest = DateTime.UtcNow.Date.AddDays(3);
            if (date.Date < earliest)
            {
                TempData["AgendaError"] = $"Earliest allowed date is {earliest:dd MMM yyyy}.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots", adminId });
            }

            // Parse time safely regardless of locale
            if (!TimeSpan.TryParse(start, out var startTs) &&
                !TimeSpan.TryParseExact(start, @"hh\:mm", null, out startTs))
            {
                TempData["AgendaError"] = "Invalid start time. Use HH:MM.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots", adminId });
            }

            if (durationMinutes <= 0 || durationMinutes > 8 * 60)
            {
                TempData["AgendaError"] = "Invalid duration.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots", adminId });
            }

            try
            {
                await _agenda.CreateAvailabilityBlockAsync(adminId.Value, date, startTs, durationMinutes);
                TempData["AgendaOk"] = "Availability block created.";
            }
            catch (Exception ex)
            {
                TempData["AgendaError"] = $"Could not create availability block: {ex.Message}";
            }

            // Keep the user on the tabbed page, Slots open
            return RedirectToAction(nameof(Agenda), new { tab = "Slots", adminId });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlot(int id, int? adminId = null)
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

            // keep user on the tabbed page
            return RedirectToAction(nameof(Agenda), new { tab = "Slots", adminId });
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
