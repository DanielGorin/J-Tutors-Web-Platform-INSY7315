
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;

namespace J_Tutors_Web_Platform.Models.Files
{
    public class FileAccess
    {
        [Key]
        public int FileAccessID { get; set; }
        public int FileID { get; set; }
        public File File { get; set; } = default!;
        public int UserID { get; set; }
        public Users.User User { get; set; } = default!;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
