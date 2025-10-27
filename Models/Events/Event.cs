using J_Tutors_Web_Platform.Models.Shared;
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.Events
{
    // Represents an event created by an admin
    public class Event
    {
        // Primary key
        [Key]
        public int EventID { get; set; }

        // Linked admin who created the event
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        // Event title
        [Required, MaxLength(150)]
        public string Title { get; set; } = default!;

        // Event details or description
        [MaxLength(2000)]
        public string? Description { get; set; }

        // Optional image for the event
        [MaxLength(500)]
        public string? ImageURL { get; set; }

        // Location of the event
        [MaxLength(200)]
        public string? Location { get; set; }

        // Date and time details
        public DateTime EventDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public int DurationMinutes { get; set; }

        // Participation and rewards
        public int GoalParticipants { get; set; }
        public int PointsReward { get; set; }

        // Optional WhatsApp group link
        [MaxLength(500)]
        public string? WhatsappGroupUrl { get; set; }

        // Current event status 
        public EventStatus Status { get; set; } = EventStatus.Draft;

        // Timestamps
        public DateTime CreationDate { get; set; }
        public DateTime UpdateDate { get; set; }

        // Navigation
        public ICollection<EventParticipation> Participations { get; set; } = new List<EventParticipation>();
    }
}
