
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.Files
{
    public class File
    {
        [Key]
        public int FileID { get; set; }
        public int AdminID { get; set; }
        public Admins.Admin Admin { get; set; } = default!;

        [Required, MaxLength(255)]
        public string FileName { get; set; } = default!;

        [Required, MaxLength(100)]
        public string ContentType { get; set; } = default!;
        [MaxLength(500)]
        public string? StorageKeyOrUrl { get; set; }
        public long? SizeBytes { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public ICollection<FileAccess> FileAccesses { get; set; } = new List<FileAccess>();
    }
}
