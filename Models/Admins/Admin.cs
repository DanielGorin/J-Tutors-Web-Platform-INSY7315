#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using J_Tutors_Web_Platform.Models.AppFiles;
using Microsoft.EntityFrameworkCore;

namespace J_Tutors_Web_Platform.Models.Admins
{
    // Makes sure each admin username is unique
    [Index(nameof(Username), IsUnique = true)]
    public class Admin
    {
        // Primary key
        [Key]
        public int AdminID { get; set; }

        // Login username
        [Required, MaxLength(50)]
        public string Username { get; set; } = default!;

        // Hashed password for security
        [Required, MaxLength(200)]
        public string PasswordHash { get; set; } = default!;

        // Salt used for hashing the password
        [Required, MaxLength(200)]
        public string PasswordSalt { get; set; } = default!;

        // Stores the admin’s chosen theme (e.g., light or dark)
        [MaxLength(50)]
        public string? ThemePreference { get; set; }

        // Relationships with other entities
        public ICollection<Events.Event> Events { get; set; } = new List<Events.Event>();
        public ICollection<Scheduling.AvailabilityBlock> AvailabilityBlocks { get; set; } = new List<Scheduling.AvailabilityBlock>();
        public ICollection<Scheduling.TutoringSession> TutoringSessions { get; set; } = new List<Scheduling.TutoringSession>();
        public ICollection<Subjects.PricingRule> PricingRules { get; set; } = new List<Subjects.PricingRule>();
        public ICollection<AppFile> Files { get; set; } = new List<AppFile>();
        public ICollection<Points.PointsReceipt> IssuedPointsReceipts { get; set; } = new List<Points.PointsReceipt>();
    }
}
