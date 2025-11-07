/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * AnalyricsController
 * File Purpose:
 * This controller is used for the admin's analytics page which shows a variety of sattistics that support the admins management of the system including (global points, average attendance, unpaid sessiosn total and monthly earnings)
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */

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
