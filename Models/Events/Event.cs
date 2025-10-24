
using J_Tutors_Web_Platform.Models.Shared;
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.Events
{
    public class Event
    {
        [Key]
        public int EventID { get; set; }

        // FK: Admin (creator/host)
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        [Required, MaxLength(150)]
        public string Title { get; set; } = default!;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? ImageURL { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        public DateTime EventDate { get; set; }          
        public TimeSpan StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public int GoalParticipants { get; set; }
        public int PointsReward { get; set; }

        [MaxLength(500)]
        public string? WhatsappGroupUrl { get; set; }

        public EventStatus Status { get; set; } = EventStatus.Draft;

        public DateTime CreationDate { get; set; }
        public DateTime UpdateDate { get; set; }

        // Navigation
        public ICollection<EventParticipation> Participations { get; set; } = new List<EventParticipation>();
    }
}
