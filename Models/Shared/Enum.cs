#nullable enable
using System;

namespace J_Tutors_Web_Platform.Models.Shared
{
    // Status of a tutoring session
    public enum TutoringSessionStatus
    {
        Requested = 0, 
        Accepted = 1,
        Denied = 2,
        Paid = 3,            // Payment completed
        Cancelled = 4
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
