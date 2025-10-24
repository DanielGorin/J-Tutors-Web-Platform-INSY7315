#nullable enable
using System;

namespace J_Tutors_Web_Platform.Models.Shared
{
    // Adjust values to our preset states as needed.
    public enum TutoringSessionStatus 
    {
        Scheduled = 0,
        Completed = 1,
        Cancelled = 2,
        NoShow = 3,
        PendingPayment = 4,
        Paid = 5
    }

    public enum EventStatus
    {
        Draft = 0,
        Published = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum PointsReceiptType
    {
        Earned = 0,      // e.g., attending an event
        Spent = 1,       // e.g., discount on a session
        Adjustment = 2   // manual correction
    }
}
