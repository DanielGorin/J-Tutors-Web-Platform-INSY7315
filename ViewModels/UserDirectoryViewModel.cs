namespace J_Tutors_Web_Platform.ViewModels
{
    public class UserDirectoryRow 
    {
        public string Username { get; set; } = default!;
        public int UnpaidSessions { get; set; }
        public double UnpaidAmount { get; set; }
        public int CurrentPoints { get; set; }
        public int TotalPoints { get; set; }
        public DateTime LastActivity { get; set; }
        public bool LeaderboardVisible { get; set; }
    }
    public class UserDirectoryViewModel
    {
        public List<UserDirectoryRow> UDR { get; set; } = new List<UserDirectoryRow>();

    }
}
