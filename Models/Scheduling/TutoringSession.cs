
using J_Tutors_Web_Platform.Models.Shared;
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace J_Tutors_Web_Platform.Models.Scheduling
{
    public class TutoringSession
    {
        [Key]
        public int TutoringSessionID { get; set; }
        public int UserID { get; set; }
        public Users.User User { get; set; } = default!;
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;
        public int SubjectID { get; set; }
        public Subjects.Subject Subject { get; set; } = default!;

        public DateTime SessionDate { get; set; }

        [Column(TypeName = "decimal(4,2)")]
        public decimal DurationHours { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal BaseCost { get; set; }

        public int PointsSpent { get; set; }

        public TutoringSessionStatus Status { get; set; } = TutoringSessionStatus.Scheduled;

        public DateTime? CancellationDate { get; set; }
        public DateTime? PaidDate { get; set; }
    }
}
