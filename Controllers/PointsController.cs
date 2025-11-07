/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * PointsController
 * File Purpose:
 * This is a controller used by the points system part of the website, this includes 
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */
#nullable enable
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using J_Tutors_Web_Platform.Services;
using System.Security.Claims;

namespace J_Tutors_Web_Platform.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("[controller]")]
    public sealed class PointsController : ControllerBase
    {
        // -------------------------
        // DEPENDENCIES
        // -------------------------
        private readonly PointsService _points;
        private readonly UserLedgerService _ledger;
        private readonly AdminService _adminService;

        // -------------------------
        // CTOR
        // -------------------------
        public PointsController(PointsService points, UserLedgerService ledger, AdminService adminService)
        {
            _points = points;
            _ledger = ledger;
            _adminService = adminService;
        }

        // -------------------------
        //  GET: GetTotals (Gets total points of specific user)
        // -------------------------
        [HttpGet("totals")]
        public async Task<IActionResult> GetTotals([FromQuery] int userId)
        {
            // Ask the PointsService to compute both values.
            var total = await _points.GetTotal(userId);
            var current = await _points.GetCurrent(userId);

            // Respond with a simple JSON object for easy UI consumption.
            return Ok(new { userId, total, current });
        }

        // -------------------------
        //  GET: GetReceipts (Gets list of receipts belonging to user)
        // -------------------------
        [HttpGet("receipts")]
        public async Task<IActionResult> GetReceipts([FromQuery] int userId)
        {
            var rows = await _ledger.GetReceiptRowsAsync(userId);
            return Ok(rows); // returns List<UserLedgerRowViewModel>
        }

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

        // -------------------------
        // POST: Adjust (admin manually asjusting points from user)
        // -------------------------
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
