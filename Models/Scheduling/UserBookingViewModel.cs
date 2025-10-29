#nullable enable
using System;
using System.Collections.Generic;
using J_Tutors_Web_Platform.Services;

namespace J_Tutors_Web_Platform.Controllers
{
    public sealed class UserBookingViewModel
    {
        public List<ActiveSubjectDto> Subjects { get; set; } = new();
        public int? SelectedSubjectID { get; set; }

        public decimal HoursPerSession { get; set; } = 1.0m;
        public int SessionCount { get; set; } = 1;
        public decimal PointsPercent { get; set; } = 0m;

        public decimal? HourlyRate { get; set; }
        public decimal? MinHours { get; set; }
        public decimal? MaxHours { get; set; }
        public decimal? MaxPointDiscount { get; set; }

        public QuoteResult? Quote { get; set; }
        public List<SlotOption> AvailableSlots { get; set; } = new();
    }
}
