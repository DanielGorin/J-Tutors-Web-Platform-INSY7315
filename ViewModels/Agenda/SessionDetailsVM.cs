#nullable enable
namespace J_Tutors_Web_Platform.ViewModels
{
    public sealed class SessionDetailsVM
    {
        public int TutoringSessionID { get; set; }
        public string Status { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public DateOnly SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public decimal DurationHours { get; set; }

        public int UserID { get; set; }
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";
        public string Email { get; set; } = "";
        public string RequestingFullName => $"{FirstName} {Surname}".Trim();

        public decimal BaseCost { get; set; }
        public int PointsSpent { get; set; }
        public decimal FinalRand => BaseCost - PointsSpent < 0 ? 0 : BaseCost - PointsSpent;

        public decimal UnpaidRandForUser { get; set; }
    }
}
