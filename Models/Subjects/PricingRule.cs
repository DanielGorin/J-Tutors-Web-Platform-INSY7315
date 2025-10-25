#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace J_Tutors_Web_Platform.Models.Subjects
{
    // Defines pricing rules for a subject set by an admin
    public class PricingRule
    {
        // Primary key
        [Key]
        public int PricingRuleID { get; set; }

        // FK: Subject
        public int SubjectID { get; set; }
        public Subjects.Subject Subject { get; set; } = default!;

        // FK: Admin who created the pricing rule
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        // Hourly rate for the subject
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; }

        // Minimum allowed hours per session
        [Column(TypeName = "decimal(4,2)")]
        public decimal MinHours { get; set; }

        // Maximum allowed hours per session
        [Column(TypeName = "decimal(4,2)")]
        public decimal MaxHours { get; set; }

        // Maximum points discount that can be applied
        [Column(TypeName = "decimal(5,2)")]
        public decimal MaxPointDiscount { get; set; }

        // Navigation
    }
}
