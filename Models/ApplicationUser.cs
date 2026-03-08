using Microsoft.AspNetCore.Identity;

namespace InkVault.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public DateTime? DateOfBirth { get; set; }
        public string? ProfilePicturePath { get; set; }
        public string? Bio { get; set; } // Max 200 characters, emoji-supported
        public string? OTP { get; set; }
        public DateTime? OTPExpiration { get; set; }
        public DateTime? OTPSentAt { get; set; }
        public int OTPResendCount { get; set; } = 0;
        public DateTime? OTPResendLockedUntil { get; set; }
        public bool EmailVerified { get; set; }
        public bool HasCompletedFirstLogin { get; set; } = false;
        public string ThemePreference { get; set; } = "dark";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LastBirthdayEmailSent { get; set; }

        // Friend relationships
        public ICollection<Friend>? FriendsInitiated { get; set; } = new List<Friend>();
        public ICollection<Friend>? FriendsReceived { get; set; } = new List<Friend>();

        // Friend requests
        public ICollection<FriendRequest>? FriendRequestsSent { get; set; } = new List<FriendRequest>();
        public ICollection<FriendRequest>? FriendRequestsReceived { get; set; } = new List<FriendRequest>();

        // Journal views
        public ICollection<JournalView>? JournalViews { get; set; } = new List<JournalView>();

        // Likes and comments
        public ICollection<Like>? Likes { get; set; } = new List<Like>();
        public ICollection<Comment>? Comments { get; set; } = new List<Comment>();

        // Saved journals
        public ICollection<SavedJournal>? SavedJournals { get; set; } = new List<SavedJournal>();

        // Notification preferences
        public NotificationPreference? NotificationPreference { get; set; }
    }
}
