using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace J_Tutors_Web_Platform.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly AnalyticsService _as;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _as = analyticsService;
        }

        public IActionResult AnalyticsPage()
        {
            var analyticsViewModel = new AnalyticsViewModel
            {
                GlobalPoints = _as.CalculateGlobalPoints(),
                AverageAttendance = _as.CalculateAverageAttendance(),
                AmountUnpaid = _as.CalculateAmountUnpaid(),
                MonthlyEarnings = _as.MonthlyRevenue()
            };

            return View("~/Views/Admin/AAnalytics.cshtml", analyticsViewModel);
        }
    }
}
