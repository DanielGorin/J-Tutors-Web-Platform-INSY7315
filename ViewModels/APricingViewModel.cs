#nullable enable
using System.Collections.Generic;
using J_Tutors_Web_Platform.Services;

namespace J_Tutors_Web_Platform.ViewModels
{
    public class APricingViewModel
    {
        public List<AdminService.SubjectRow> Subjects { get; set; } = new();
        public int? SelectedSubjectID { get; set; }

        public AdminService.PricingRuleRow? Pricing { get; set; }

        // Convenience for binding the pricing form
        public decimal? HourlyRate { get; set; }
        public decimal? MinHours { get; set; }
        public decimal? MaxHours { get; set; }
        public decimal? MaxPointDiscount { get; set; }
    }
}
