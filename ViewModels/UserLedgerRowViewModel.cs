#nullable enable
using System;
using System.Collections.Generic;

namespace J_Tutors_Web_Platform.ViewModels
{
    public enum LedgerRowKind { Earned = 0, Spent = 1, Adjustment = 2 }

    /// Row model: only fields we display
    public sealed class UserLedgerRowViewModel
    {
        public int PointsReceiptID { get; set; }
        public DateTime ReceiptDateUtc { get; set; }   // stored UTC in DB
        public LedgerRowKind Kind { get; set; }        // Earned / Spent / Adjustment
        public int Amount { get; set; }                // include sign (+/-)
    }

    /// Page model: just the rows (no totals, no extras)
    public sealed class UserLedgerPageViewModel
    {
        public int UserId { get; set; }
        public List<UserLedgerRowViewModel> Rows { get; set; } = new();
    }
}
