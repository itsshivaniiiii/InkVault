using InkVault.Models;

namespace InkVault.ViewModels
{
    public class FriendJournalViewModel
    {
        public int JournalId { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string AuthorName { get; set; } = null!;
        public string? AuthorProfilePicture { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ViewCount { get; set; }
        public string? Topic { get; set; }
        public List<string>? Tags { get; set; }
        public PrivacyLevel PrivacyLevel { get; set; }
        public string? DUI { get; set; }
        public string? ReferencedDUI { get; set; }

        public int ReadingTimeMinutes =>
            Math.Max(1, (int)Math.Ceiling(
                System.Text.RegularExpressions.Regex.Replace(Content ?? string.Empty, "<[^>]+>", " ")
                    .Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length / 200.0
            ));
    }

    public class FriendsFeedViewModel
    {
        public List<FriendJournalViewModel> PublicJournals { get; set; } = new List<FriendJournalViewModel>();
        public List<FriendJournalViewModel> FriendsOnlyJournals { get; set; } = new List<FriendJournalViewModel>();
        public bool HasFriends { get; set; }
    }
}
