#nullable enable
using System;

namespace J_Tutors_Web_Platform.ViewModels
{
    public sealed class AdminUserDirectoryRowViewModel
    {
        public int UserID { get; set; }
        public string Username { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";
        public DateTime BirthDate { get; set; }
        public bool LeaderboardVisible { get; set; }

        public int PointsTotal { get; set; }      // timeframe-applied
        public int PointsCurrent { get; set; }    // timeframe-applied
        public decimal UnpaidRandTotal { get; set; } // all-time outstanding
    }
}
