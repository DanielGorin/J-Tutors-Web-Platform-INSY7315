#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.AppFiles
{
    // Represents a record of a user being given access to a file
    public class FileShareAccess
    {
        // Primary key
        [Key]
        public int FileAccessID { get; set; }

        // Linked file
        public int FileID { get; set; }
        public AppFile File { get; set; } = default!;

        // Linked user who has access
        public int UserID { get; set; }
        public Users.User User { get; set; } = default!;

        // Access start date
        public DateTime StartDate { get; set; }

        // Access end date 
        public DateTime? EndDate { get; set; }
    }
}
