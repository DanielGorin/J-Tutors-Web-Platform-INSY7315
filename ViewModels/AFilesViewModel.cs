using J_Tutors_Web_Platform.Models.AppFiles;

namespace J_Tutors_Web_Platform.ViewModels
{
    public class FileShareRow
    {
        public string FileName { get; set; } = default!;
        public string AdminUsername { get; set; } = default!;
        public int UserCount { get; set; }

    }
    public class AFilesViewModel
    {
        public List<AppFile> appFile { get; set; } = new List<AppFile>();
        public List<FileShareAccess> FSA { get; set; } = new List<FileShareAccess>();
        public List<FileShareRow> FSR { get; set; } = new List<FileShareRow>();
    }
}
