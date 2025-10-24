
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.Events
{
    public class EventParticipation
    {
        [Key]
        public int EventParticipationID { get; set; }

        // FK: Event
        public int EventID { get; set; }
        public Event Event { get; set; } = default!;

        // FK: User
        public int UserID { get; set; }
        public Users.User User { get; set; } = default!;

        public DateTime JoinDate { get; set; }

        [MaxLength(50)]
        public string? Attendance { get; set; } // free text or map to enum later
    }
}
