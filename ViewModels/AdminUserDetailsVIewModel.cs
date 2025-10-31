#nullable enable
namespace J_Tutors_Web_Platform.ViewModels
{
    public sealed class AdminUserDetailsViewModel
    {
        // Identity
        public int UserID { get; set; }
        public string Username { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";

        // Contacts & preferences
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? SubjectInterest { get; set; }
        public bool LeaderboardVisible { get; set; }
        public string? ThemePreference { get; set; }

        // Dates
        public DateTime? BirthDate { get; set; }
        public DateTime? RegistrationDate { get; set; }

        // Points snapshot
        public int PointsTotal { get; set; }
        public int PointsCurrent { get; set; }
    }
}
