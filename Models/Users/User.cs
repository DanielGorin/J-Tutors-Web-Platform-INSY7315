#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace J_Tutors_Web_Platform.Models.Users
{
    // Represents a user of the tutoring platform
    [Index(nameof(Username), IsUnique = true)]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        // Primary key
        [Key]
        public int UserID { get; set; }

        // Login username
        [Required, MaxLength(50)]
        public string Username { get; set; } = default!;

        // Hashed password
        [Required, MaxLength(200)]
        public string PasswordHash { get; set; } = default!;

        // Salt used for hashing the password
        [Required, MaxLength(200)]
        public string PasswordSalt { get; set; } = default!;

        // User's first name
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = default!;

        // User's surname
        [Required, MaxLength(100)]
        public string Surname { get; set; } = default!;

        // Optional phone number
        [MaxLength(30)]
        public string? Phone { get; set; }

        // User's email (unique)
        [Required, MaxLength(256)]
        public string Email { get; set; } = default!;

        // Date of birth
        public DateTime BirthDate { get; set; }

        // Subjects the user is interested in
        [MaxLength(200)]
        public string? SubjectInterest { get; set; }

        // Controls visibility on leaderboard
        public bool LeaderboardVisible { get; set; } = true;

        // Optional UI theme preference
        [MaxLength(50)]
        public string? ThemePreference { get; set; }

        // Account registration date
        public DateTime RegistrationDate { get; set; }

        // Last login date (optional)
        public DateTime? LastLogin { get; set; }

        // Navigation
        public ICollection<Events.EventParticipation> EventParticipations { get; set; } = new List<Events.EventParticipation>();
        public ICollection<Scheduling.TutoringSession> TutoringSessions { get; set; } = new List<Scheduling.TutoringSession>();
        public ICollection<Points.PointsReceipt> PointsReceipts { get; set; } = new List<Points.PointsReceipt>();
        public ICollection<AppFiles.FileShareAccess> FileAccesses { get; set; } = new List<AppFiles.FileShareAccess>();
    }
}
