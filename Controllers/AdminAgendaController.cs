#nullable enable
using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using J_Tutors_Web_Platform.Models.Scheduling;
using System.Collections.Generic;

namespace J_Tutors_Web_Platform.Controllers
{
    // Re-enable auth once roles are wired end-to-end
    [Authorize(Roles = "Admin")]
    public sealed class AdminAgendaController : Controller
    {
        private readonly AdminAgendaService _agenda;
        private readonly AdminService _adminService;

        public AdminAgendaController(AdminAgendaService agenda, AdminService adminService)
        {
            _agenda = agenda;
            _adminService = adminService;
        }

        // -------------------------------
        // Helpers
        // -------------------------------
        private int? ResolveAdminId()
        {
            // Use the same claim as the rest of your Admin area (AdminController)
            var username = User?.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrWhiteSpace(username)) return null;

            var id = _adminService.GetAdminID(username);
            return id > 0 ? id : null;
        }

        // Prefer 24h HH:mm, accept H:mm, and fallback to general TimeSpan parse
        private static bool TryParseHHmm(string value, out TimeSpan ts)
        {
            return TimeSpan.TryParseExact(value, "HH\\:mm", CultureInfo.InvariantCulture, out ts)
                || TimeSpan.TryParseExact(value, "H\\:mm", CultureInfo.InvariantCulture, out ts)
                || TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out ts);
        }

        // Keep for potential future range filters (not used for "ALL slots" mode)
        private static (DateTime from, DateTime to) NormalizeRange(DateTime? from, DateTime? to)
        {
            var todayLocal = DateTime.Today;
            var f = from?.Date ?? todayLocal;
            var t = to?.Date ?? f.AddDays(14); // 2-week default window
            if (t < f) (f, t) = (t, f);
            return (f, t);
        }

        // --------------------------------------------------------------------
        // Landing page (tabbed): Agenda (Slots / Inbox / Calendar)
        // Route: GET /AdminAgenda/Agenda?tab=Slots|Inbox|Calendar
        // --------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Agenda(
            string? tab = null,
            DateTime? from = null,
            DateTime? to = null,
            int? year = null,
            int? month = null,
            bool includeRequested = true)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
            {
                TempData["AgendaError"] = "Admin not identified. Please sign in again.";
                return RedirectToAction("Login", "Account");
            }

            tab ??= "Slots";
            ViewBag.AdminId = aid.Value;
            ViewBag.ActiveTab = tab;



            // Header counts + inbox (used to render badges in the AAgenda view)
            var inbox = await _agenda.GetInboxAsync(aid.Value);
            var requested = inbox?.Requested?.Count ?? 0;
            var accepted = inbox?.Accepted?.Count ?? 0;
            var paid = inbox?.Paid?.Count ?? 0;
            var cancelled = inbox?.Cancelled?.Count ?? 0;


            var inboxDisplay = await _agenda.GetInboxDisplayAsync(aid.Value);
            // Defaults per tab
            var yy = year ?? DateTime.Now.Year;      // local time semantics
            var mm = month ?? DateTime.Now.Month;

            var first = new DateTime(yy, mm, 1);
            var next = first.AddMonths(1);
            ViewBag.Slots = await _agenda.GetAvailabilityBlocksAsync(first, next, aid.Value);

            var pageVm = new AAgendaPageVM
            {
                ActiveTab = tab,
                RequestedCount = requested,
                AcceptedCount = accepted,
                PaidCount = paid,
                CancelledCount = cancelled,

                Inbox = inbox,
                InboxDisplay = inboxDisplay,

                // SLOTS TAB: show ALL slots for the admin
                Slots = new AgendaSlotsVM
                {
                    From = null,
                    To = null,
                    Blocks = await _agenda.GetAvailabilityBlocksAsync(null, null, aid.Value)
                },

                // CALENDAR TAB: month sessions; slots will be supplied via ViewBag in Calendar() action as well
                Calendar = new AgendaCalendarVM
                {
                    Year = yy,
                    Month = mm,
                    IncludeRequested = includeRequested,
                    Sessions = await _agenda.GetSessionsForCalendarAsync(yy, mm, includeRequested, aid.Value)
                }
            };

            return View("~/Views/Admin/AAgenda.cshtml", pageVm);
        }

        // --------------------------------------------------------------------
        // SLOTS (read)
        // GET /AdminAgenda/Slots
        // --------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Slots(DateTime? from = null, DateTime? to = null)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
            {
                TempData["AgendaError"] = "Admin not identified.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            // Show ALL slots (ignore from/to)
            var vm = new AgendaSlotsVM
            {
                From = null,
                To = null,
                Blocks = await _agenda.GetAvailabilityBlocksAsync(null, null, aid.Value)
            };

            ViewBag.AdminId = aid.Value;
            return View("~/Views/Admin/AAgendaSlots.cshtml", vm);
        }

        // --------------------------------------------------------------------
        // SLOTS (create)
        // POST /AdminAgenda/CreateSlot
        // form: date (yyyy-MM-dd), start (HH:mm), durationMinutes (int)
        // --------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSlot(DateTime date, string start, int durationMinutes)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
            {
                TempData["AgendaError"] = "Admin not identified.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            if (string.IsNullOrWhiteSpace(start) || !TryParseHHmm(start, out var startTs))
            {
                TempData["AgendaError"] = "Start time format must be HH:mm (e.g., 09:00).";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            if (durationMinutes < 15 || durationMinutes > 720 || durationMinutes % 15 != 0)
            {
                TempData["AgendaError"] = "Duration must be a multiple of 15 between 15 and 720 minutes.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            try
            {
                await _agenda.CreateAvailabilityBlockAsync(aid.Value, date.Date, startTs, durationMinutes);
                TempData["AgendaOk"] = "Availability slot created.";
            }
            catch (Exception)
            {
                // log ex if you have logging wired
                TempData["AgendaError"] = "Could not create slot. Please try again.";
            }

            return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
        }

        // --------------------------------------------------------------------
        // SLOTS (delete)
        // POST /AdminAgenda/DeleteSlot
        // --------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlot(int id)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
            {
                TempData["AgendaError"] = "Admin not identified.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            if (id <= 0)
            {
                TempData["AgendaError"] = "Invalid slot id.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            try
            {
                await _agenda.DeleteAvailabilityBlockAsync(id);
                TempData["AgendaOk"] = "Availability slot deleted.";
            }
            catch (Exception)
            {
                TempData["AgendaError"] = "Could not delete slot. Please try again.";
            }

            return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
        }

        // --------------------------------------------------------------------
        // INBOX — grouped by status (Requested / Accepted / Paid / Cancelled)
        // GET /AdminAgenda/Inbox
        // --------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Inbox()
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
            {
                TempData["AgendaError"] = "Admin not identified.";
                return RedirectToAction(nameof(Agenda), new { tab = "Inbox" });
            }

            var vm = await _agenda.GetInboxDisplayAsync(aid.Value);
            ViewBag.AdminId = aid.Value;
            return View("~/Views/Admin/AAgendaInbox.cshtml", vm);
        }

        // --------------------------------------------------------------------
        // CALENDAR — sessions for month (optionally incl. Requested)
        // GET /AdminAgenda/Calendar?year=YYYY&month=MM&includeRequested=true|false
        // --------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Calendar(int? year = null, int? month = null, bool includeRequested = true)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
            {
                TempData["AgendaError"] = "Admin not identified.";
                return RedirectToAction(nameof(Agenda), new { tab = "Calendar" });
            }

            var yy = year ?? DateTime.Now.Year;
            var mm = month ?? DateTime.Now.Month;

            var first = new DateTime(yy, mm, 1);
            var next = first.AddMonths(1);

            var sessions = await _agenda.GetSessionsForCalendarAsync(yy, mm, includeRequested, aid.Value);

            // Provide availability slots for this month to the view (used by toggles)
            ViewBag.Slots = await _agenda.GetAvailabilityBlocksAsync(first, next, aid.Value);

            var vm = new AgendaCalendarVM
            {
                Year = yy,
                Month = mm,
                IncludeRequested = includeRequested,
                Sessions = sessions
            };

            ViewBag.AdminId = aid.Value;
            return View("~/Views/Admin/AAgendaCalendar.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> SessionDetails(int id)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
                return Unauthorized();

            var vm = await _agenda.GetSessionDetailsAsync(id);
            if (vm is null) return NotFound();

            // Optional safety: ensure the session belongs to this admin
            // (uncomment if you want to enforce)
            // var belongs = await _agenda.SessionBelongsToAdminAsync(id, aid.Value);
            // if (!belongs) return Forbid();

            return PartialView("~/Views/Admin/AAgendaSessionDetails.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0) return Unauthorized();

            var res = await _agenda.UpdateSessionStatusAsync(id, "Accepted");
            var vm = await _agenda.GetSessionDetailsAsync(id);
            if (vm is null) return NotFound();

            Response.Headers["X-Action-Result"] = res.Ok ? "ok" : "error";
            Response.Headers["X-Action-Message"] = res.Message;
            return PartialView("~/Views/Admin/AAgendaSessionDetails.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deny(int id)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0) return Unauthorized();

            var res = await _agenda.UpdateSessionStatusAsync(id, "Denied");
            var vm = await _agenda.GetSessionDetailsAsync(id);
            if (vm is null) return NotFound();

            Response.Headers["X-Action-Result"] = res.Ok ? "ok" : "error";
            Response.Headers["X-Action-Message"] = res.Message;
            return PartialView("~/Views/Admin/AAgendaSessionDetails.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0) return Unauthorized();

            var res = await _agenda.UpdateSessionStatusAsync(id, "Cancelled");
            var vm = await _agenda.GetSessionDetailsAsync(id);
            if (vm is null) return NotFound();

            Response.Headers["X-Action-Result"] = res.Ok ? "ok" : "error";
            Response.Headers["X-Action-Message"] = res.Message;
            return PartialView("~/Views/Admin/AAgendaSessionDetails.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0) return Unauthorized();

            var res = await _agenda.UpdateSessionStatusAsync(id, "Paid");
            var vm = await _agenda.GetSessionDetailsAsync(id);
            if (vm is null) return NotFound();

            Response.Headers["X-Action-Result"] = res.Ok ? "ok" : "error";
            Response.Headers["X-Action-Message"] = res.Message;
            return PartialView("~/Views/Admin/AAgendaSessionDetails.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> InboxLists()
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0) return Unauthorized();

            var vm = await _agenda.GetInboxDisplayAsync(aid.Value);
            return PartialView("~/Views/Admin/AAgendaInboxLists.cshtml", vm);
        }



    }
}
