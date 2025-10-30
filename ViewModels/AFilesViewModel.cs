using J_Tutors_Web_Platform.Models.AppFiles;

namespace J_Tutors_Web_Platform.ViewModels
{
    public class FileShareRow
    {
        public string FileName { get; set; } = default!;
        public string AdminUsername { get; set; } = default!;
        public int UserCount { get; set; }

    }
    public class FileShareAccessRow
    {
        public int FileAccessID { get; set; } = default!;
        public int FileID { get; set; } = default!;
        public string FileName { get; set; } = default!;
        public int UserID { get; set; } = default!;
        public string Username { get; set; } = default!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
    }
    public class AFilesViewModel
    {
        public List<AppFile> appFile { get; set; } = new List<AppFile>();
        public List<FileShareAccess> FSA { get; set; } = new List<FileShareAccess>();
        public List<FileShareRow> FSR { get; set; } = new List<FileShareRow>();
        public List<FileShareAccessRow> FSAR { get; set; } = new List<FileShareAccessRow>();
        public int CurrentFileID { get; set; }
    }
}
