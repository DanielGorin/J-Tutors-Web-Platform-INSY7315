using System.ComponentModel.DataAnnotations;

namespace J_Tutors_Web_Platform.Models.Users
{
    /**
     * user profile view model
     * carries profile data to and from the view
     */
    public class UserProfileViewModel
    {
        //SEGMENT read-only display fields
        //-------------------------------------------------------------------------------------------
        public string? FirstName { get; set; }      // shown only
        public string? Surname { get; set; }        // shown only
        public DateOnly? BirthDate { get; set; }    // shown only
        public DateOnly? RegistrationDate { get; set; } // shown only
        //-------------------------------------------------------------------------------------------

        //SEGMENT editable fields
        //-------------------------------------------------------------------------------------------
        [Required, Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [EmailAddress, Display(Name = "Email")]
        public string? Email { get; set; }

        [Phone, Display(Name = "Phone")]
        public string? Phone { get; set; }

        [Display(Name = "Subject Interest")]
        public string? SubjectInterest { get; set; }

        [Display(Name = "Show me on Leaderboard")]
        public bool LeaderboardVisible { get; set; }

        [Display(Name = "Theme")]
        public string? ThemePreference { get; set; }
        //-------------------------------------------------------------------------------------------
    }
}
