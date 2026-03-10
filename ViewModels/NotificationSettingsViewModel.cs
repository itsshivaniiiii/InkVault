using System.ComponentModel.DataAnnotations;

namespace InkVault.ViewModels
{
    public class NotificationSettingsViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Friend Request Received")]
        public bool EmailOnFriendRequestReceived { get; set; }

        [Display(Name = "Friend Request Accepted")]
        public bool EmailOnFriendRequestAccepted { get; set; }

        [Display(Name = "Friend Request Denied")]
        public bool EmailOnFriendRequestDenied { get; set; }

        [Display(Name = "Friend Posts New Journal")]
        public bool EmailOnFriendJournalPost { get; set; }

        [Display(Name = "Someone Likes Your Journal")]
        public bool EmailOnJournalLiked { get; set; }

        [Display(Name = "Someone Comments on Your Journal")]
        public bool EmailOnJournalCommented { get; set; }

        [Display(Name = "Require OTP on Every Login")]
        public bool RequireOTPOnEveryLogin { get; set; }

        [Display(Name = "Email Notification on Successful Login")]
        public bool EmailOnSuccessfulLogin { get; set; }

        [Display(Name = "Success Message")]
        public string SuccessMessage { get; set; } = "";
    }
}
