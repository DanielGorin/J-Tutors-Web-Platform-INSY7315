#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.Scheduling
{
    // Represents a time block when an admin is available for tutoring
    public class AvailabilityBlock
    {
        // Primary key
        [Key]
        public int AvailabilityBlockID { get; set; }

        // FK: Admin who owns this block
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        // Date and time range of availability
        public DateTime BlockDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Navigation
    }
}
