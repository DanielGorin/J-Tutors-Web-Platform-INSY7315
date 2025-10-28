using J_Tutors_Web_Platform.Models.Scheduling;

namespace J_Tutors_Web_Platform.ViewModels
{
    public class ASessionsCalenderViewModel
    {
        public List<TutoringSession> TutoringSessions { get; set; }
        public List<AvailabilityBlock> AvailabilityBlock { get; set; }
    }
}