#nullable enable
using System;
using System.Collections.Generic;

namespace J_Tutors_Web_Platform.ViewModels
{
    public enum AdminDirectoryTimeframe
    {
        ThisMonth,
        Last30Days,
        LastMonth,
        Last4Months,
        AllTime
    }

    public sealed class AdminUserDirectoryPageViewModel
    {
        // Data
        public List<AdminUserDirectoryRowViewModel> Rows { get; set; } = new();

        // Filters / state
        public string? Search { get; set; }
        public AdminDirectoryTimeframe Timeframe { get; set; } = AdminDirectoryTimeframe.ThisMonth;

        // Sorting
        public string SortColumn { get; set; } = "Username"; // Username|FirstName|Surname|PointsTotal|PointsCurrent|UnpaidRandTotal|BirthDate|LeaderboardVisible
        public string SortDirection { get; set; } = "ASC";   // ASC|DESC

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalRows { get; set; } = 0;

        // Convenience
        public int ShowingFrom => TotalRows == 0 ? 0 : ((Page - 1) * PageSize + 1);
        public int ShowingTo => Math.Min(Page * PageSize, TotalRows);
    }
}
