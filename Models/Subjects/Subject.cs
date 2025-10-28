#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace J_Tutors_Web_Platform.Models.Subjects
{
    // Represents a subject that can be taught in tutoring sessions
    [Index(nameof(SubjectName), IsUnique = true)]
    public class Subject
    {
        // Primary key
        [Key]
        public int SubjectID { get; set; }

        // Name of the subject 
        [Required, MaxLength(100)]
        public string SubjectName { get; set; } = default!;

        // Indicates if the subject is currently active
        public bool IsAvtive { get; set; } = true;

        // Navigation
        public ICollection<PricingRule> PricingRules { get; set; } = new List<PricingRule>();
        public ICollection<Scheduling.TutoringSession> TutoringSessions { get; set; } = new List<Scheduling.TutoringSession>();
    }
}
