using AspNetCoreGeneratedDocument;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace J_Tutors_Web_Platform.Controllers
{
    public class EventController : Controller
    {
        private readonly EventService _as;

        public EventController(EventService eventService)
        {
            _as = eventService;
        }

        [HttpGet]
        public IActionResult GetAEventList()
        {
            var eventViewModel = new EventViewModel()
            {
                DetailedEventRows = _as.GetEvents()
            };

            return View("~/Views/Admin/AEventList.cshtml", eventViewModel);
        }

        [HttpGet]
        public IActionResult GetUEventList()
        {
            var eventViewModel = new EventViewModel()
            {
                DetailedEventRows = _as.GetUserEvents(User.FindFirst(ClaimTypes.Name)?.Value)
            };

            return View("~/Views/User/UEvents.cshtml", eventViewModel);
        }

        [HttpGet]
        public IActionResult GetUEventHistory()
        {
            var eventViewModel = new EventViewModel()
            {
                DetailedEventRows = _as.GetUserEventHistory(User.FindFirst(ClaimTypes.Name)?.Value)
            };

            return View("~/Views/User/UEventHistory.cshtml", eventViewModel);
        }

        public IActionResult CreateEvent(string Title, string description, string Location, DateOnly EventDate, TimeOnly StartTime, int DurationMinutes, int PointsReward, int GoalParticipants, string WhatsappGroupURL, string Status)
        {
            string username = User.FindFirst(ClaimTypes.Name)?.Value;
            int adminID = _as.GetAdminID(username!);
            DateOnly creationDate = DateOnly.FromDateTime(DateTime.Now);
            DateOnly updateDate = DateOnly.FromDateTime(DateTime.Now);
            string imageURL = "https://via.placeholder.com/150"; // Placeholder image URL

            Console.WriteLine("reached EventController CreateEvent");

            _as.CreateEvent(adminID, Title, description, imageURL, Location, EventDate, StartTime, DurationMinutes, PointsReward, GoalParticipants, WhatsappGroupURL, Status, creationDate, updateDate);

            return RedirectToAction("GetAEventList");
        }

        public IActionResult AEventDetails(int EventID)
        {
            string username = User.FindFirst(ClaimTypes.Name)?.Value;
            var eventViewModel = new EventViewModel()
            {
                Events = _as.GetEventDetails(EventID, username)
            };

            return View("~/Views/Admin/AEventDetails.cshtml", eventViewModel);
        }

        public IActionResult UpdateEvent(int EventID,string Title, string Description, string Location, DateOnly EventDate, TimeOnly StartTime, int DurationMinutes, int PointsReward, int GoalParticipants, string WhatsappGroupURL, string Status)
        {
            string username = User.FindFirst(ClaimTypes.Name)?.Value;
            int adminID = _as.GetAdminID(username!);
            DateOnly updateDate = DateOnly.FromDateTime(DateTime.Now);
            string imageURL = "https://via.placeholder.com/150"; // Placeholder image URL

            Console.WriteLine("reached EventController CreateEvent");

            _as.UpdateEvent(EventID, adminID, Title, Description, imageURL, Location, EventDate, StartTime, DurationMinutes, PointsReward, GoalParticipants, WhatsappGroupURL, Status, updateDate);

            return RedirectToAction("GetAEventList");
        }

        public IActionResult JoinEvent(int EventID)
        {
            Console.WriteLine("in controller with " + EventID);

            string username = User.FindFirst(ClaimTypes.Name)?.Value;

            _as.JoinEvent(EventID, username);

            var eventViewModel = new EventViewModel()
            {
                DetailedEventRows = _as.GetUserEvents(User.FindFirst(ClaimTypes.Name)?.Value)
            };

            return View("~Views/Admin/UEventHistory.cshtml", eventViewModel);
        }
    }
}
