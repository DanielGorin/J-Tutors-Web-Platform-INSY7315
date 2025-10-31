#nullable enable
namespace J_Tutors_Web_Platform.ViewModels
{
    public sealed class AgendaInboxRowVM
    {
        public int TutoringSessionID { get; set; }

        // Display fields
        public string SubjectName { get; set; } = "";
        public decimal DurationHours { get; set; }

        public string RequestingFullName { get; set; } = ""; // "FirstName Surname"

        // Prices
        public decimal BaseCost { get; set; }     // stored in TutoringSession
        public int PointsSpent { get; set; }      // stored in TutoringSession
        public decimal PriceRand => BaseCost - PointsSpent < 0 ? 0 : BaseCost - PointsSpent;
        public int PricePoints => PointsSpent;

        // For badge
        public string Status { get; set; } = "";  // "Requested" | "Accepted" | "Paid" | "Cancelled"
    }

    public sealed class AgendaInboxDisplayVM
    {
        public IReadOnlyList<AgendaInboxRowVM> Requested { get; set; } = new List<AgendaInboxRowVM>();
        public IReadOnlyList<AgendaInboxRowVM> Accepted { get; set; } = new List<AgendaInboxRowVM>();
        public IReadOnlyList<AgendaInboxRowVM> Paid { get; set; } = new List<AgendaInboxRowVM>();
        public IReadOnlyList<AgendaInboxRowVM> Cancelled { get; set; } = new List<AgendaInboxRowVM>();
    }
}
