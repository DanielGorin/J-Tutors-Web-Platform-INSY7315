using J_Tutors_Web_Platform.Models.Events;
using J_Tutors_Web_Platform.Models.Shared;

namespace J_Tutors_Web_Platform.ViewModels
{
    public class DetailedEventRow
    {
        public int EventID { get; set; }
        public int AdminID { get; set; }
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public string? ImageURL { get; set; }
        public string? ImageTitle { get; set;  }
        public string? ImageDescription { get; set; }
        public string? Location { get; set; }
        public DateOnly EventDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public int GoalParticipants { get; set; }
        public int PointsReward { get; set; }
        public string? WhatsappGroupUrl { get; set; }
        public EventStatus Status { get; set; } = EventStatus.Draft;
        public DateOnly CreationDate { get; set; }
        public DateOnly UpdateDate { get; set; }
        public int CurrentParticipants { get; set; }

    }
    public class EventViewModel
    {
        public List<Event> Events { get; set; } = new List<Event>();
        public List<EventParticipation> EventParticipations { get; set; } = new List<EventParticipation>();
        public List<DetailedEventRow> DetailedEventRows { get; set; } = new List<DetailedEventRow>();
    }
}
