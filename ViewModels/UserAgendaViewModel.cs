using System;
using System.Collections.Generic;
using J_Tutors_Web_Platform.Models.Scheduling;

namespace J_Tutors_Web_Platform.ViewModels
{
    // PAGE VM for the calendar view
    public sealed class UserAgendaViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; } // 1-12
        public bool IncludeRequested { get; set; } = true;

        public IReadOnlyList<TutoringSession> Sessions { get; set; }
            = Array.Empty<TutoringSession>();
    }

    // READ-ONLY details VM (what the user sees on click)
    public sealed class UserSessionDetailsVM
    {
        public int TutoringSessionID { get; set; }

        // Minimal fields requested
        public string Status { get; set; } = "";
        public DateOnly SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public decimal DurationHours { get; set; }

        public decimal FinalRand { get; set; }   // BaseCost - PointsSpent, min 0
        public int PointsSpent { get; set; }
    }
}
