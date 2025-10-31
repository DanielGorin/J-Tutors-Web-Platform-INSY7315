#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.ViewModels
{
    public enum LeaderboardViewMode { Current, Total }
    public enum LeaderboardTimeFilter { ThisMonth, Last30Days, LastMonth, AllTime }

    public class LeaderboardRowVM
    {
        public int UserID { get; set; }
        public string Username { get; set; } = "";
        public int Rank { get; set; }
        public int EarnedInPeriod { get; set; } // only positive receipts in window
        public int NetAllTime { get; set; }     // includes spending (negatives) where AffectsAllTime=true
        public bool IsCurrentUser { get; set; }
        public bool IsVisibleOptIn { get; set; }
        public int TotalEarned { get; set; }      // sum of all positive receipts (lifetime)
        public int EarnedThisMonth { get; set; }  // sum of positive receipts in the current month

    }

    public class LeaderboardFiltersVM
    {
        public LeaderboardViewMode Mode { get; set; } = LeaderboardViewMode.Current;
        public LeaderboardTimeFilter Time { get; set; } = LeaderboardTimeFilter.ThisMonth;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class LeaderboardPageVM
    {
        public LeaderboardFiltersVM Filters { get; set; } = new();
        public List<LeaderboardRowVM> Rows { get; set; } = new();
        public int TotalRows { get; set; }
        public int ShowingFrom { get; set; }
        public int ShowingTo { get; set; }
    }
}
