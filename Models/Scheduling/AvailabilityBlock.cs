
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.Scheduling
{
    public class AvailabilityBlock
    {
        [Key]
        public int AvailabilityBlockID { get; set; }


        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        public DateTime BlockDate { get; set; }          
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
