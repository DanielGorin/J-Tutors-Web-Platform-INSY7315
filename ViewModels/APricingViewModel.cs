#nullable enable
using System.Collections.Generic;
using J_Tutors_Web_Platform.Models.Subjects;
using J_Tutors_Web_Platform.Services;

namespace J_Tutors_Web_Platform.ViewModels
{
    public class APricingViewModel
    {
        public List<Subject> Subjects { get; set; } = new();
        public int? SelectedSubjectID { get; set; }

        public PricingRule? Pricing { get; set; }

        public decimal? HourlyRate { get; set; }
        public decimal? MinHours { get; set; }
        public decimal? MaxHours { get; set; }
        public decimal? MaxPointDiscount { get; set; }
    }
}
