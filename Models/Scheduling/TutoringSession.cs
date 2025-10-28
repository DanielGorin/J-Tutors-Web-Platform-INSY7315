using J_Tutors_Web_Platform.Models.Shared;
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace J_Tutors_Web_Platform.Models.Scheduling
{
    // Represents a scheduled tutoring session between a user and an admin
    public class TutoringSession
    {
        // Primary key
        [Key]
        public int TutoringSessionID { get; set; }

        // FK: User attending the session
        public int UserID { get; set; }
        public Users.User User { get; set; } = default!;

        // FK: Admin hosting the session
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        // FK: Subject for this session
        public int SubjectID { get; set; }
        public Subjects.Subject Subject { get; set; } = default!;

        // Date of the session
        public DateTime SessionDate { get; set; }

        // Start time of the session
        public DateTime StartTime { get; set; }

        // Duration in hours
        [Column(TypeName = "decimal(4,2)")]
        public decimal DurationHours { get; set; }

        // Base cost for the session
        [Column(TypeName = "decimal(10,2)")]
        public decimal BaseCost { get; set; }

        // Points used by the user for this session
        public int PointsSpent { get; set; }

        // Current status of the session 
        public TutoringSessionStatus Status { get; set; } = TutoringSessionStatus.Scheduled;

        // Optional timestamps
        public DateTime? CancellationDate { get; set; }
        public DateTime? PaidDate { get; set; }

        // Navigation
    }
}
