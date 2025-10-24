
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace YourApp.Models.Scheduling
{
    namespace J_Tutors_Web_Platform.Models.Scheduling
    {
        [Key]
        public int AvailabilityBlockID { get; set; }

        // FK: Admin
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        public DateTime BlockDate { get; set; }          
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
