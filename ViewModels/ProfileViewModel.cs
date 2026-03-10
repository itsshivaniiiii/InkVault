using System.ComponentModel.DataAnnotations;

namespace InkVault.ViewModels
{
    public class ProfileViewModel
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Username { get; set; } = null!;

        public string UserId { get; set; } = null!;

        [Phone]
        public string? PhoneNumber { get; set; }

        public string? Gender { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public string? ProfilePictureUrl { get; set; }

        [StringLength(200, ErrorMessage = "Bio must not exceed 200 characters.")]
        public string? Bio { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public string ThemePreference { get; set; } = "dark";

        // For public profile
        public bool AreFriends { get; set; }
        public int PublicJournalCount { get; set; } = 0;
        public bool IsBlockedByMe { get; set; }
        public bool HasBlockedMe { get; set; }
        public List<UserJournalViewModel> PinnedJournals { get; set; } = new List<UserJournalViewModel>();
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = null!;

        [Required]
        [StringLength(100, ErrorMessage = "Password must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = null!;

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }

    public class UserJournalViewModel
    {
        public int JournalId { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string? Topic { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ViewCount { get; set; }
        public InkVault.Models.PrivacyLevel PrivacyLevel { get; set; }
        public bool IsAnonymous { get; set; }
        public string? DUI { get; set; }
        public string? ReferencedDUI { get; set; }

        public int ReadingTimeMinutes =>
            Math.Max(1, (int)Math.Ceiling(
                System.Text.RegularExpressions.Regex.Replace(Content ?? string.Empty, "<[^>]+>", " ")
                    .Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length / 200.0
            ));
    }

    public class UserJournalsViewModel
    {
        public string UserId { get; set; } = null!;
        public string UserFirstName { get; set; } = null!;
        public string UserLastName { get; set; } = null!;
        public string? UserProfilePicture { get; set; }
        public bool IsOwner { get; set; }
        public bool AreFriends { get; set; }
        public List<UserJournalViewModel> Journals { get; set; } = new List<UserJournalViewModel>();
        public int JournalCount { get; set; }
    }

    public class BlockedUserViewModel
    {
        public string BlockedUserId { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? ProfilePicturePath { get; set; }
        public DateTime BlockedAt { get; set; }
    }
}

