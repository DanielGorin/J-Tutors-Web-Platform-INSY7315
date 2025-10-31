#nullable enable
using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using J_Tutors_Web_Platform.Models.Scheduling;

namespace J_Tutors_Web_Platform.Controllers
{
    // =========================================================
    // CONTROLLER: AdminAgendaController
    // Purpose: admin-facing agenda screen (slots, inbox, calendar)
    // =========================================================
    [Authorize(Roles = "Admin")]
    public sealed class AdminAgendaController : Controller
    {
        // -----------------------------------------
        // DEPENDENCIES
        // _agenda       - handles agenda logic (slots, sessions)
        // _adminService - used here mainly to resolve adminID from  username
        // -----------------------------------------
        private readonly AdminAgendaService _agenda;
        private readonly AdminService _adminService;

        // -----------------------------------------
        // CTOR: DI entry point
        // -----------------------------------------
        public AdminAgendaController(AdminAgendaService agenda, AdminService adminService)
        {
            _agenda = agenda;
            _adminService = adminService;
        }

        // =========================================================
        // =============== HELPEr / UTILITY METHODS =================
        // These are small single-purpose helpers used by many actions.
        // =========================================================

        // ---------------------------------------------------------
        // ResolveAdminId()
        // Goal to  turn the logged-in user's name/claim into adminId
        //  ff it cannot find then return null
        // ---------------------------------------------------------
        private int? ResolveAdminId()
        {
            // Get the username from the current principal (logged-in user)
            var username = User?.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrWhiteSpace(username)) return null;

            // Ask the AdminService to turn that username into an AdminID
            var id = _adminService.GetAdminID(username);

            // If positive → we consider it valid
            return id > 0 ? id : null;
        }

        // ---------------------------------------------------------
        // TryParseHHmm()
        // Goal: be lenient about time format
        // Accepts:
        //   - "HH:mm"  (e.g. "09:30")
        //   - "H:mm"   (e.g. "9:30")
        //   - and if all else fails, a generic TimeSpan parse
        // ---------------------------------------------------------
        private static bool TryParseHHmm(string value, out TimeSpan ts)
        {
            return TimeSpan.TryParseExact(value, "HH\\:mm", CultureInfo.InvariantCulture, out ts)
                || TimeSpan.TryParseExact(value, "H\\:mm", CultureInfo.InvariantCulture, out ts)
                || TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out ts);
        }

        // ---------------------------------------------------------
        // NormalizeRange()
        // Goal: when we have optional from/to, make sure we get a valid window
        // Currently: defaults to "today - +14 days"
        // NOTE: Not heavily used for "all slots" mode, but kept for future
        // ---------------------------------------------------------
        private static (DateTime from, DateTime to) NormalizeRange(DateTime? from, DateTime? to)
        {
            var todayLocal = DateTime.Today;
            var f = from?.Date ?? todayLocal;
            var t = to?.Date ?? f.AddDays(14); // 2-week default window
            if (t < f) (f, t) = (t, f);       // swap if in wrong order
            return (f, t);
        }

        // =========================================================
        // =================== LANDING / AGENDA =====================
        // GET /AdminAgenda/Agenda
        // This is the "hub" view which shows:
        //  - active tab (Slots / Inbox / Calendar)
        //  - inbox counts (pending/accepted/paid/cancelled)
        //  - month calendar data
        //  - all slots (for the Slots tab)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Agenda(
            string? tab = null,
            DateTime? from = null,
            DateTime? to = null,
            int? year = null,
            int? month = null,
            bool includeRequested = true)
        {
            // 1. make sure we know which admin is calling this
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
            {
                TempData["AgendaError"] = "Admin not identified. Please sign in again.";
                return RedirectToAction("Login", "Account");
            }

            // 2. default tab if not supplied
            tab ??= "Slots";
            ViewBag.AdminId = aid.Value;
            ViewBag.ActiveTab = tab;

            // -------------------------------------------------
            // INBOX: used to show how many items are in each state
            // -------------------------------------------------
            var inbox = await _agenda.GetInboxAsync(aid.Value);
            var requested = inbox?.Requested?.Count ?? 0;
            var accepted = inbox?.Accepted?.Count ?? 0;
            var paid = inbox?.Paid?.Count ?? 0;
            var cancelled = inbox?.Cancelled?.Count ?? 0;

            // also a display-friendly version of inbox
            var inboxDisplay = await _agenda.GetInboxDisplayAsync(aid.Value);

            // -------------------------------------------------
            // Calendar defaults (year/month)
            // If none given → use current
            // -------------------------------------------------
            var yy = year ?? DateTime.Now.Year;
            var mm = month ?? DateTime.Now.Month;

            // we load this month’s slots so the Calendar tab can show them
            var first = new DateTime(yy, mm, 1);
            var next = first.AddMonths(1);
            ViewBag.Slots = await _agenda.GetAvailabilityBlocksAsync(first, next, aid.Value);

            // -------------------------------------------------
            // Build page VM with all 3 logical parts:
            // 1) Inbox area
            // 2) Slots tab
            // 3) Calendar tab
            // -------------------------------------------------
            var pageVm = new AAgendaPageVM
            {
                ActiveTab = tab,

                // counts for top-level badges
                RequestedCount = requested,
                AcceptedCount = accepted,
                PaidCount = paid,
                CancelledCount = cancelled,

                // inbox content
                Inbox = inbox,
                InboxDisplay = inboxDisplay,

                // SLOTS TAB: show ALL slots for the admin
                Slots = new AgendaSlotsVM
                {
                    From = null,
                    To = null,
                    Blocks = await _agenda.GetAvailabilityBlocksAsync(null, null, aid.Value)
                },

                // CALENDAR TAB: sessions for that month
                Calendar = new AgendaCalendarVM
                {
                    Year = yy,
                    Month = mm,
                    IncludeRequested = includeRequested,
                    Sessions = await _agenda.GetSessionsForCalendarAsync(yy, mm, includeRequested, aid.Value)
                }
            };

            // use explicit view path so we know exactly what view is rendered
            return View("~/Views/Admin/AAgenda.cshtml", pageVm);
        }

        // =========================================================
        // =================== SLOTS: READ =========================
        // GET /AdminAgenda/Slots
        // Show ALL availability blocks for current admin
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Slots(DateTime? from = null, DateTime? to = null)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
            {
                TempData["AgendaError"] = "Admin not identified.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            // NOTE: for now we ignore from/to and just show all
            var vm = new AgendaSlotsVM
            {
                From = null,
                To = null,
                Blocks = await _agenda.GetAvailabilityBlocksAsync(null, null, aid.Value)
            };

            ViewBag.AdminId = aid.Value;
            return View("~/Views/Admin/AAgendaSlots.cshtml", vm);
        }

        // =========================================================
        // =================== SLOTS: CREATE =======================
        // POST /AdminAgenda/CreateSlot
        // Expects form fields:
        //   date (yyyy-MM-dd)
        //   start (HH:mm)
        //   durationMinutes (int, multiple of 15)
        // =========================================================
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

            // validate time format first (admin UX)
            if (string.IsNullOrWhiteSpace(start) || !TryParseHHmm(start, out var startTs))
            {
                TempData["AgendaError"] = "Start time format must be HH:mm (e.g., 09:00).";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            // validate duration (business rule)
            if (durationMinutes < 15 || durationMinutes > 720 || durationMinutes % 15 != 0)
            {
                TempData["AgendaError"] = "Duration must be a multiple of 15 between 15 and 720 minutes.";
                return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
            }

            try
            {
                // call service to actually create slot
                await _agenda.CreateAvailabilityBlockAsync(aid.Value, date.Date, startTs, durationMinutes);
                TempData["AgendaOk"] = "Availability slot created.";
            }
            catch (Exception)
            {
                // TODO: log if logging available
                TempData["AgendaError"] = "Could not create slot. Please try again.";
            }

            return RedirectToAction(nameof(Agenda), new { tab = "Slots" });
        }

        // =========================================================
        // =================== SLOTS: DELETE =======================
        // POST /AdminAgenda/DeleteSlot
        // =========================================================
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

            // simple validation
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

        // =========================================================
        // ===================== INBOX: VIEW =======================
        // GET /AdminAgenda/Inbox
        // Returns grouped sessions:
        //   - Requested
        //   - Accepted
        //   - Paid
        //   - Cancelled
        // =========================================================
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

        // =========================================================
        // =================== CALENDAR: VIEW ======================
        // GET /AdminAgenda/Calendar
        // Shows a single month of sessions (+ optionally requested ones)
        // Also pushes that month’s availability via ViewBag
        // =========================================================
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

            // window for availability
            var first = new DateTime(yy, mm, 1);
            var next = first.AddMonths(1);

            // sessions for this month
            var sessions = await _agenda.GetSessionsForCalendarAsync(yy, mm, includeRequested, aid.Value);

            // slots for this month (used for toggle/display)
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

        // =========================================================
        // =================== SESSION DETAILS =====================
        // GET /AdminAgenda/SessionDetails/{id}
        // Returns partial for one session (used in modals or side-panels)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> SessionDetails(int id)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0)
                return Unauthorized();

            var vm = await _agenda.GetSessionDetailsAsync(id);
            if (vm is null) return NotFound();

            // If you want to enforce ownership, uncomment below
            // var belongs = await _agenda.SessionBelongsToAdminAsync(id, aid.Value);
            // if (!belongs) return Forbid();

            return PartialView("~/Views/Admin/AAgendaSessionDetails.cshtml", vm);
        }

        // =========================================================
        // ================== SESSION ACTIONS ======================
        // These actions change the status of a session:
        //  - Accept
        //  - Deny
        //  - Cancel
        //  - MarkPaid
        // They all:
        //  1. check admin
        //  2. update via service
        //  3. fetch latest details
        //  4. return the partial again
        //  5. set headers with result for JS
        // =========================================================

        // -------------------------
        // POST: Accept
        // -------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var aid = ResolveAdminId();
            if (aid is null || aid <= 0) return Unauthorized();

            var res = await _agenda.UpdateSessionStatusAsync(id, "Accepted");
            var vm = await _agenda.GetSessionDetailsAsync(id);
            if (vm is null) return NotFound();

            // headers for client-side notification
            Response.Headers["X-Action-Result"] = res.Ok ? "ok" : "error";
            Response.Headers["X-Action-Message"] = res.Message;

            return PartialView("~/Views/Admin/AAgendaSessionDetails.cshtml", vm);
        }

        // -------------------------
        // POST: Deny
        // -------------------------
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

        // -------------------------
        // POST: Cancel
        // -------------------------
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

        // -------------------------
        // POST: MarkPaid
        // -------------------------
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

        // =========================================================
        // ========== INBOX LISTS (PARTIAL RELOAD SUPPORT) =========
        // GET /AdminAgenda/InboxLists
        // Allows refreshing just the inbox lists without reloading page
        // =========================================================
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
