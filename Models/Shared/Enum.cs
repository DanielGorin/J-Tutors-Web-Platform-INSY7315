#nullable enable
using System;

namespace J_Tutors_Web_Platform.Models.Shared
{
    // Status of a tutoring session
    public enum TutoringSessionStatus
    {
        Scheduled = 0,      // Session is scheduled
        Completed = 1,      // Session has been completed
        Cancelled = 2,      // Session was cancelled
        NoShow = 3,         // User did not show up
        PendingPayment = 4, // Awaiting payment
        Paid = 5            // Payment completed
    }

    // Status of an event
    public enum EventStatus
    {
        Draft = 0,       // Event is being drafted
        Published = 1,   // Event is live/published
        Completed = 2,   // Event has finished
        Cancelled = 3    // Event was cancelled
    }

    // Type of points transaction
    public enum PointsReceiptType
    {
        Earned = 0,      // Points earned 
        Spent = 1,       // Points spent 
        Adjustment = 2   // Manual correction of points
    }
}
