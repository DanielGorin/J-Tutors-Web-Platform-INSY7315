
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace J_Tutors_Web_Platform.Models.Users
{
    [Index(nameof(Username), IsUnique = true)]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        public int UserID { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = default!;

        [Required, MaxLength(200)]
        public string PasswordHash { get; set; } = default!;

        [Required, MaxLength(200)]
        public string PasswordSalt { get; set; } = default!;

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = default!;

        [Required, MaxLength(100)]
        public string Surname { get; set; } = default!;

        [MaxLength(30)]
        public string? Phone { get; set; }

        [Required, MaxLength(256)]
        public string Email { get; set; } = default!;

        public DateTime BirthDate { get; set; }

        [MaxLength(200)]
        public string? SubjectInterest { get; set; }

        public bool LeaderboardVisible { get; set; } = true;

        [MaxLength(50)]
        public string? ThemePreference { get; set; }

        public DateTime RegistrationDate { get; set; }
        public DateTime? LastLogin { get; set; }

        public ICollection<Events.EventParticipation> EventParticipations { get; set; } = new List<Events.EventParticipation>();
        public ICollection<Scheduling.TutoringSession> TutoringSessions { get; set; } = new List<Scheduling.TutoringSession>();
        public ICollection<Points.PointsReceipt> PointsReceipts { get; set; } = new List<Points.PointsReceipt>();
        public ICollection<AppFiles.FileShareAccess> FileAccesses { get; set; } = new List<AppFiles.FileShareAccess>();
    }
}
