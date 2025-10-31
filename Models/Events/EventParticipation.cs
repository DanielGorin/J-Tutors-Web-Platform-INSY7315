#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.Events
{
    // Represents a user's participation in an event
    public class EventParticipation
    {
        // Primary key 
        [Key]
        public int EventParticipationID { get; set; }

        // FK: Event
        public int EventID { get; set; }
        public Event Event { get; set; } = default!;

        // FK: User
        public int UserID { get; set; }
        public Users.User User { get; set; } = default!;

        // Date the user joined the event
        public DateTime JoinDate { get; set; }

        // Attendance status or notes
        [MaxLength(50)]
        public string? Attendance { get; set; }

        // Navigation
    }
}
