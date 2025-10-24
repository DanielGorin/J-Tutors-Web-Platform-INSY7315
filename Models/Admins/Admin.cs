#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace J_Tutors_Web_Platform.Models.Admins
{
    [Index(nameof(Username), IsUnique = true)]
    public class Admin
    {
        [Key]
        public int AdminID { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = default!;

        [Required, MaxLength(200)]
        public string PasswordHash { get; set; } = default!;

        [Required, MaxLength(200)]
        public string PasswordSalt { get; set; } = default!;

        [MaxLength(50)]
        public string? ThemePreference { get; set; }

        public ICollection<Events.Event> Events { get; set; } = new List<Events.Event>();
        public ICollection<Scheduling.AvailabilityBlock> AvailabilityBlocks { get; set; } = new List<Scheduling.AvailabilityBlock>();
        public ICollection<Scheduling.TutoringSession> TutoringSessions { get; set; } = new List<Scheduling.TutoringSession>();
        public ICollection<Subjects.PricingRule> PricingRules { get; set; } = new List<Pricing.PricingRule>();
        public ICollection<Files.AppFile> Files { get; set; } = new List<Files.AppFile>();
        public ICollection<Points.PointsReceipt> IssuedPointsReceipts { get; set; } = new List<Points.PointsReceipt>();
    }
}
