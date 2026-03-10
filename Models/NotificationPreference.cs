using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkVault.Models
{
    public class NotificationPreference
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public string UserId { get; set; }

        public ApplicationUser User { get; set; }

        // Friends Notifications
        [Display(Name = "Friend Request Received")]
        public bool EmailOnFriendRequestReceived { get; set; } = true;

        [Display(Name = "Friend Request Accepted")]
        public bool EmailOnFriendRequestAccepted { get; set; } = true;

        [Display(Name = "Friend Request Denied")]
        public bool EmailOnFriendRequestDenied { get; set; } = false;

        [Display(Name = "Friend Posts New Journal")]
        public bool EmailOnFriendJournalPost { get; set; } = true;

        [Display(Name = "Someone Likes Your Journal")]
        public bool EmailOnJournalLiked { get; set; } = false;

        [Display(Name = "Someone Comments on Your Journal")]
        public bool EmailOnJournalCommented { get; set; } = false;

        // Security & Login Notifications
        [Display(Name = "Require OTP on Every Login")]
        public bool RequireOTPOnEveryLogin { get; set; } = false;

        [Display(Name = "Email Notification on Successful Login")]
        public bool EmailOnSuccessfulLogin { get; set; } = true;

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
