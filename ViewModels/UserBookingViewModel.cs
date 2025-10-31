using System;
using System.Collections.Generic;

namespace J_Tutors_Web_Platform.ViewModels
{
    public sealed class UserBookingViewModel
    {
        public IReadOnlyList<SubjectListItemVM> Subjects { get; set; } = Array.Empty<SubjectListItemVM>();
        public int? SelectedSubjectId { get; set; }
        public int? UserPointsBalance { get; set; } // optional
    }

    public sealed class SubjectListItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // Pricing/config for the selected subject
    public sealed class SubjectConfigVM
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = "";

        public decimal HourlyRate { get; set; }            // R/hour
        public int MinDurationMinutes { get; set; }         // from PricingRule.MinHours * 60
        public int MaxDurationMinutes { get; set; }         // from PricingRule.MaxHours * 60
        public int DurationStepMinutes { get; set; } = 30;

        public int MaxDiscountPercent { get; set; }         // floor(PricingRule.MaxPointDiscount)
        public int DiscountStepPercent { get; set; } = 10;
    }

    // Quote/price preview
    public sealed class QuoteVM
    {
        public decimal BaseCost { get; set; }
        public int DiscountPercentApplied { get; set; }
        public int PointsToCharge { get; set; }
        public decimal MoneyDiscount { get; set; }
        public decimal FinalCost { get; set; }
    }

    // Availability for a month
    public sealed class AvailabilityMonthVM
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public IReadOnlyList<DayAvailabilityVM> Days { get; set; } = Array.Empty<DayAvailabilityVM>();
    }

    public sealed class DayAvailabilityVM
    {
        public int Day { get; set; } // 1..31
        public IReadOnlyList<SlotVM> Slots { get; set; } = Array.Empty<SlotVM>();
    }

    // One availability block + its valid start times for the chosen duration
    public sealed class SlotVM
    {
        public int AvailabilityBlockId { get; set; }
        public DateOnly BlockDate { get; set; }
        public TimeSpan BlockStart { get; set; }
        public TimeSpan BlockEnd { get; set; }
        public IReadOnlyList<TimeOptionVM> StartOptions { get; set; } = Array.Empty<TimeOptionVM>();
    }

    // A single start time option within a block
    public sealed class TimeOptionVM
    {
        public DateOnly SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    // Payload posted from UBooking form
    public sealed class BookingRequestVM
    {
        public int SubjectId { get; set; }
        public int DurationMinutes { get; set; }   // 30-min steps
        public int DiscountPercent { get; set; }   // 0..Max, step 10
        public DateOnly SessionDate { get; set; }  // "YYYY-MM-DD" (model binder must support DateOnly)
        public string StartTime { get; set; } = ""; // "HH:mm"
    }

    public sealed class BookingResult
    {
        public bool Ok { get; set; }
        public string? Message { get; set; }
        public int? BookingId { get; set; }
    }
}
