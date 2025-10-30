using System;
using System.Collections.Generic;
using J_Tutors_Web_Platform.Models.Scheduling;

namespace J_Tutors_Web_Platform.ViewModels
{
    public sealed class AAgendaPageVM
    {
        // Header counts
        public int ScheduledCount { get; set; }
        public int AcceptedCount { get; set; }
        public int PaidCount { get; set; }
        public int CancelledCount { get; set; }

        // Current tab: "Slots", "Inbox", or "Calendar"
        public string ActiveTab { get; set; } = "Slots";

        // Child view models for the three tabs
        public AgendaSlotsVM? Slots { get; set; }
        public AgendaInboxVM? Inbox { get; set; }
        public AgendaCalendarVM? Calendar { get; set; }
    }

    public sealed class AgendaSlotsVM
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? Minutes { get; set; }

        public IReadOnlyList<AvailabilityBlock> Blocks { get; set; }
            = Array.Empty<AvailabilityBlock>();
    }

    public sealed class AgendaInboxVM
    {
        public IReadOnlyList<TutoringSession> Scheduled { get; set; }
            = Array.Empty<TutoringSession>();
        public IReadOnlyList<TutoringSession> Accepted { get; set; }
            = Array.Empty<TutoringSession>();
        public IReadOnlyList<TutoringSession> Paid { get; set; }
            = Array.Empty<TutoringSession>();
        public IReadOnlyList<TutoringSession> Cancelled { get; set; }
            = Array.Empty<TutoringSession>();
    }

    public sealed class AgendaCalendarVM
    {
        public int Year { get; set; }
        public int Month { get; set; } // 1-12
        public bool IncludeScheduled { get; set; }

        public IReadOnlyList<TutoringSession> Sessions { get; set; }
            = Array.Empty<TutoringSession>();
    }
}
