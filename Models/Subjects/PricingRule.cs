#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace J_Tutors_Web_Platform.Models.Subjects
{
    public class PricingRule
    {
        [Key]
        public int PricingRuleID { get; set; }

        public int SubjectID { get; set; }
        public Subjects.Subject Subject { get; set; } = default!;

        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; }

        [Column(TypeName = "decimal(4,2)")]
        public decimal MinHours { get; set; }

        [Column(TypeName = "decimal(4,2)")]
        public decimal MaxHours { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal MaxPointDiscount { get; set; }
    }
}
