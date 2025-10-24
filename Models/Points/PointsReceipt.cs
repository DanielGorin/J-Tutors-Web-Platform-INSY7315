
using J_Tutors_Web_Platform.Models.Shared;
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace J_Tutors_Web_Platform.Models.Points
{
    public class PointsReceipt
    {
        [Key]
        public int PointsReceiptID { get; set; }

        public int UserID { get; set; }
        public Users.User User { get; set; } = default!;

        public int? EventParticipationID { get; set; }
        public Events.EventParticipation? EventParticipation { get; set; }

        [ForeignKey(nameof(Session))]
        public int? SessionID { get; set; }
        public Scheduling.TutoringSession? Session { get; set; }
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        public DateTime ReceiptDate { get; set; }

        public PointsReceiptType Type { get; set; }

        public int Amount { get; set; }

        [MaxLength(200)]
        public string? Reason { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        public bool AffectsAllTime { get; set; } = false;

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}
