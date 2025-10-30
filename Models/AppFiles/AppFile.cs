#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.AppFiles
{
    // Represents a file uploaded by an admin
    public class AppFile
    {

        // Primary key
        [Key]
        public int FileID { get; set; }

        // Linked admin who uploaded the file
        public int AdminID { get; set; }
        
        [Required, MaxLength(255)]
        public string FileName { get; set; } = default!;
        
        [Required, MaxLength(100)]
        public string ContentType { get; set; } = default!;

    }
}
