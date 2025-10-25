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
        public Admins.Admin Admin { get; set; } = default!;

        
        [Required, MaxLength(255)]
        public string FileName { get; set; } = default!;

        
        [Required, MaxLength(100)]
        public string ContentType { get; set; } = default!;

        // Path or URL where the file is stored
        [MaxLength(500)]
        public string? StorageKeyOrUrl { get; set; }

        // File size in bytes
        public long? SizeBytes { get; set; }

        // Date and time when the file was uploaded
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // List of users who have access to this file
        public ICollection<FileShareAccess> FileAccesses { get; set; } = new List<FileShareAccess>();
    }
}
