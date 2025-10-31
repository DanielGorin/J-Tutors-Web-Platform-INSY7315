#nullable enable
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using J_Tutors_Web_Platform.Services;
using System.Security.Claims;

namespace J_Tutors_Web_Platform.Controllers
{
    // ====================================================================================
    // POINTS API (ADMIN-ONLY)
    // ====================================================================================
    //
    // What this controller is for:
    //
    //  - Small, focused JSON endpoints for reading and modifying "points receipts".
    //  - These endpoints DO NOT run the core business flows for booking or agenda.
    //    Those flows already call PointsService directly inside their own services.
    //
    // What is exposed here:
    //
    //  1) GET totals      -> Current and All-Time totals for a specific user.
    //  2) GET receipts    -> Full list of that user's receipts (for a ledger/statement).
    //  3) POST adjust     -> Create a manual ADJUSTMENT (positive or negative).
    //  4) DELETE by-ref   -> Delete all receipts that share a given Reference string.
    //  5) POST spend-for-session (TEST) -> Helper to simulate a session spend (not used in prod).
    //
    // Why this is helpful for the teammate doing "Finish Event → Semi-Auto Apply Points":
    //
    //  - They can re-use the same PointsService methods from their event code.
    //  - They can give all event awards a consistent Reference pattern ("EV-{eventId}").
    //    This makes undoing mistakes easy: DELETE /Points/by-ref?reference=EV-{eventId}.
    //
    // Security:
    //  - Admin-only (role check at the controller level).
    //  - POST endpoints use anti-forgery to match the rest of the project style.
    //
    // ====================================================================================

    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("[controller]")]
    public sealed class PointsController : ControllerBase
    {
        // ----------------------------------------------------------------------
        // Dependencies (injected by DI)
        // ----------------------------------------------------------------------
        //
        // PointsService:    Core CRUD/compute logic for receipts and balances.
        // UserLedgerService:Read-only listing/totals (used by "receipts" call).
        // AdminService:     Resolve AdminID for the currently logged-in admin.
        //
        // NOTE: Keep this controller thin. Put business rules in services.
        // ----------------------------------------------------------------------

        private readonly PointsService _points;
        private readonly UserLedgerService _ledger;
        private readonly AdminService _adminService;

        public PointsController(PointsService points, UserLedgerService ledger, AdminService adminService)
        {
            _points = points;
            _ledger = ledger;
            _adminService = adminService;
        }

        // ====================================================================================
        // SECTION 1: READ TOTALS / CURRENT
        // ====================================================================================
        //
        // Endpoint: GET /Points/totals?userId=123
        //
        // Returns two numbers for the given user:
        //    - total   : All-Time style number (Earned + eligible Adjustments - negative Adjustments).
        //    - current : total minus all Spent.
        //
        // When to use:
        //   - Admin "User Details" header.
        //   - Quick checks before manual actions.
        //   - UI widgets that show a user's current balance.
        //
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Returns { total, current } for a user.
        /// </summary>
        [HttpGet("totals")]
        public async Task<IActionResult> GetTotals([FromQuery] int userId)
        {
            // Ask the PointsService to compute both values.
            var total = await _points.GetTotal(userId);
            var current = await _points.GetCurrent(userId);

            // Respond with a simple JSON object for easy UI consumption.
            return Ok(new { userId, total, current });
        }

        // ====================================================================================
        // SECTION 2: READ RECEIPTS (LEDGER)
        // ====================================================================================
        //
        // Endpoint: GET /Points/receipts?userId=123
        //
        // What it does:
        //   - Returns all the user's receipts (newest first).
        //   - Intended for a "ledger view" (think of a bank statement but for points).
        //
        // Why this exists:
        //   - Transparency for the user and admin: see exactly what happened when.
        //
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Returns all receipts for a user (newest → oldest).
        /// </summary>
        [HttpGet("receipts")]
        public async Task<IActionResult> GetReceipts([FromQuery] int userId)
        {
            var rows = await _ledger.GetReceiptRowsAsync(userId);
            return Ok(rows); // returns List<UserLedgerRowViewModel>
        }


        // ====================================================================================
        // SECTION 3: MANUAL ADJUSTMENT (+ / -)  — Admin use only
        // ====================================================================================
        //
        // Endpoint: POST /Points/adjust
        //
        // What it does:
        //   - Creates an ADJUSTMENT receipt for a single user.
        //   - Amount can be positive (grant points) or negative (remove points).
        //
        // When to use:
        //   - One-off fixes, goodwill credits, correcting mistakes.
        //   - NOT recommended for bulk event awards (do those in your event service
        //     using PointsService to create Earned receipts with a shared Reference).
        //
        // Anti-forgery:
        //   - This action expects a Form POST with a valid anti-forgery token.
        //
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Request body for POST /Points/adjust
        /// </summary>
        public sealed class AdjustDto
        {
            // Who this adjustment is for (user primary key).
            public int UserId { get; set; }

            // Positive or negative points.
            // Example: +50 to grant 50 points, -10 to remove 10 points.
            public int Amount { get; set; }

            // Short human-friendly explanation (optional).
            public string? Reason { get; set; }

            // A stable string you choose so you can group/undo later if needed.
            // Examples:
            //   "MANUAL-2025-11-01-FIX"
            //   "EV-456-EXTRA-HELPER-BONUS"
            public string? Reference { get; set; }

            // Whether this affects "All-Time" totals (defaults to true).
            // If you want something to be current-only (not historical), set false.
            public bool AffectsAllTime { get; set; } = true;

            // Optional notes (admin initials, ticket number, context).
            public string? Notes { get; set; }
        }

        /// <summary>
        /// Creates a manual ADJUSTMENT receipt (positive or negative).
        /// Returns the new receipt id and refreshed totals.
        /// </summary>
        [HttpPost("adjust")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust([FromForm] AdjustDto dto)
        {
            // -------------------------------------
            // Who is the issuing admin?
            // -------------------------------------
            // We record the AdminID for traceability. We resolve it from the logged-in user.
            var adminUsername = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var adminId = _adminService.GetAdminID(adminUsername);

            // -------------------------------------
            // Create the ADJUSTMENT via PointsService
            // -------------------------------------
            // This stores one new receipt with Type=Adjustment (your PointsService does the details).
            var id = await _points.CreateAdjustment(dto.UserId, adminId, dto.Amount, dto.Reason, dto.Reference, dto.Notes);

            if (id is null)
            {
                // Could not create the receipt — let the client know.
                return BadRequest(new { ok = false, message = "Could not create adjustment." });
            }

            // -------------------------------------
            // Return updated balances for immediate UI refresh
            // -------------------------------------
            var total = await _points.GetTotal(dto.UserId);
            var current = await _points.GetCurrent(dto.UserId);

            return Ok(new { ok = true, pointsReceiptId = id.Value, total, current });
        }

        // ====================================================================================
        // SECTION 4: DELETE BY REFERENCE (UNDO / BULK UNDO)
        // ====================================================================================
        //
        // Endpoint: DELETE /Points/by-ref?reference=TS-123
        //
        // What it does:
        //   - Deletes ALL receipts that match a given Reference string.
        //
        // Common patterns:
        //   - Session charges use "TS-{sessionId}".
        //     (AdminAgendaService calls this on Deny/Cancel to "refund" points by deletion.)
        //   - For events, we recommend "EV-{eventId}" for all Earned receipts created
        //     during the semi-auto award. If a mistake happens, you can undo in one call:
        //       DELETE /Points/by-ref?reference=EV-456
        //
        // Notes:
        //   - This is simple and blunt; it deletes rows. If you ever need audit trail,
        //     change PointsService to mark "voided" instead of DELETE.
        //
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Deletes all receipts that share the given Reference.
        /// Returns { ok, rows } where rows is the number of receipts removed.
        /// </summary>
        [HttpDelete("by-ref")]
        public async Task<IActionResult> DeleteByReference([FromQuery] string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return BadRequest(new { ok = false, message = "Reference required." });

            var rows = await _points.DeleteByReference(reference);
            return Ok(new { ok = rows > 0, rows });
        }

        // ====================================================================================
        // SECTION 5: TEST/SEED HELPER — SPEND FOR SESSION (NOT USED IN PROD BOOKING FLOW)
        // ====================================================================================
        //
        // Endpoint: POST /Points/spend-for-session
        //
        // What it does:
        //   - Manually creates a Spent receipt tied to a Session.
        //
        // When to use:
        //   - Testing or local seeding. The production booking flow already charges
        //     points inside UserBookingService inside a DB transaction; it does not
        //     call this endpoint.
        //
        // Anti-forgery:
        //   - Form POST with a token, same as /adjust.
        //
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Request body for POST /Points/spend-for-session (TEST ONLY).
        /// </summary>
        public sealed class SpendDto
        {
            public int UserId { get; set; }
            public int SessionId { get; set; }

            // Provide a positive integer; the service will store it as a negative "Spent".
            public int Amount { get; set; }
        }

        /// <summary>
        /// TEST helper to create a Spent receipt tied to a TutoringSession.
        /// Not used by the live booking flow.
        /// </summary>
        [HttpPost("spend-for-session")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SpendForSession([FromForm] SpendDto dto)
        {
            // -------------------------------------
            // Identify the issuing admin
            // -------------------------------------
            var adminUsername = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var adminId = _adminService.GetAdminID(adminUsername);

            // -------------------------------------
            // Create the SPENT receipt via PointsService
            // -------------------------------------
            var id = await _points.CreateSpentForSession(dto.UserId, adminId, dto.SessionId, dto.Amount);
            if (id is null)
                return BadRequest(new { ok = false, message = "Could not create spent receipt." });

            // -------------------------------------
            // Return updated balances for convenience
            // -------------------------------------
            var total = await _points.GetTotal(dto.UserId);
            var current = await _points.GetCurrent(dto.UserId);
            return Ok(new { ok = true, pointsReceiptId = id.Value, total, current });
        }

        // ====================================================================================
        // END OF CONTROLLER
        // ====================================================================================
    }
}
