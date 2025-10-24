
#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace J_Tutors_Web_Platform.Models.Subjects
{
    [Index(nameof(SubjectName), IsUnique = true)]
    public class Subject
    {
        [Key]
        public int SubjectID { get; set; }

        [Required, MaxLength(100)]
        public string SubjectName { get; set; } = default!;

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Subjects.PricingRule> PricingRules { get; set; } = new List<Subjects.PricingRule>();
        public ICollection<Scheduling.TutoringSession> TutoringSessions { get; set; } = new List<Scheduling.TutoringSession>();
    }
}
