#nullable enable
using System.Collections.Generic;

namespace J_Tutors_Web_Platform.Models.Test
{
    public class FileShareDemoViewModel
    {
        public string? Folder { get; set; }               // e.g., "admin" or "docs/2025"
        public List<string> Files { get; set; } = new();  // storage paths like "admin/Rules.pdf"
        public string? Message { get; set; }              // status message after upload/delete
        public string? Error { get; set; }                // error message (if any)
    }
}
