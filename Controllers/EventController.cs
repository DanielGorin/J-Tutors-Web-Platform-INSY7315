/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * EventController
 * File Purpose:
 * This is a controller used by the events side of the website
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */

using AspNetCoreGeneratedDocument;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.Services;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace J_Tutors_Web_Platform.Controllers
{
    // -------------------------
    // CONTROLLER: EventController admin/user events: list, details, create/update, join, receipts
    // -------------------------
    public class EventController : Controller
    {
        // -------------------------
        // DEPENDENCIES
        // -------------------------
        private readonly EventService _as;

        // -------------------------
        // CTOR
        // -------------------------
        public EventController(EventService eventService)
        {
            _as = eventService;
        }

        // -------------------------
        // GET: GetAEventList (A list of admin events)
        // ------------------------
        [HttpGet]
        public IActionResult GetAEventList()
        {
            var eventViewModel = new EventViewModel()
            {
                DetailedEventRows = _as.GetEvents()
            };

            return View("~/Views/Admin/AEventList.cshtml", eventViewModel);
        }

        // -------------------------
        // GET: GetUEventList (Events that are visible to the user)
        // -------------------------
        [HttpGet]
        public IActionResult GetUEventList()
        {
            var eventViewModel = new EventViewModel()
            {
                DetailedEventRows = _as.GetUserEvents(User.FindFirst(ClaimTypes.Name)?.Value)
            };

            return View("~/Views/User/UEvents.cshtml", eventViewModel);
        }

        // -------------------------
        // GET: GetUEventHistory (all historic evetns the user has signed up for)
        // -------------------------

        [HttpGet]
        public IActionResult GetUEventHistory()
        {
            var eventViewModel = new EventViewModel()
            {
                DetailedEventRows = _as.GetUserEventHistory(User.FindFirst(ClaimTypes.Name)?.Value)
            };

            return View("~/Views/User/UEventHistory.cshtml", eventViewModel);
        }

        // -------------------------
        // Action: CreateEvent (admin creates a new event)
        // -------------------------

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

        // -------------------------
        // GET: AEventDetails ( takes admin to a page showing event details + participants)
        // -------------------------
        public IActionResult AEventDetails(int EventID)
        {
            string username = User.FindFirst(ClaimTypes.Name)?.Value;
            var eventViewModel = new EventViewModel()
            {
                Events = _as.GetEventDetails(EventID, username),
                UserParticipations = _as.GetEventUsers(EventID)
            };

            return View("~/Views/Admin/AEventDetails.cshtml", eventViewModel);
        }


        // -------------------------
        // ACTION: UpdateEvent (admin updates an event)
        // -------------------------

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
        // -------------------------
        // ACTION: JoinEvent (user joins the event
        // -------------------------

        public IActionResult JoinEvent(int EventID)
        {
            //Console.WriteLine("in controller with " + EventID);

            string username = User.FindFirst(ClaimTypes.Name)?.Value;

            _as.JoinEvent(EventID, username);

            var eventViewModel = new EventViewModel()
            {
                DetailedEventRows = _as.GetUserEvents(User.FindFirst(ClaimTypes.Name)?.Value)
            };

            return View("~Views/Admin/UEventHistory.cshtml", eventViewModel);
        }

        // -------------------------
        // ACTION: DeleteUserFromEven (admin can remove a user from the event)
        // -------------------------

        public IActionResult DeleteUserFromEvent(int EventID, int UserID) 
        {
            //Console.WriteLine("entered delete user from event with: " + EventID + " " + UserID);

            _as.DeleteUserFromEvent(EventID, UserID);

            Console.WriteLine("deleted user from event");

            return RedirectToAction("AEventDetails", EventID);
        }
        // -------------------------
        // POST: GenerateReceipt (admin creates points receipt for users distributing points)
        // -------------------------
        [HttpPost]
        public IActionResult GenerateReceipt(int EventID, int UserID)
        {
            //Console.WriteLine("entered GenerateReceipt with: " + EventID + " " + UserID);

            _as.GenerateReceiptFromEvent(EventID, UserID);

            Console.WriteLine("Generated Receipt");

            return RedirectToAction("AEventDetails", EventID);
        }
    }
}
