namespace InkVault.ViewModels
{
    public class ExploreViewModel
    {
        public int JournalId { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string AuthorName { get; set; } = null!;
        public string? AuthorProfilePicture { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ViewCount { get; set; }
        public string? Topic { get; set; }
        public List<string>? Tags { get; set; }
        public string PreviewText { get; set; } = null!;
        public bool IsFriendsOnly { get; set; }
        public bool IsPublic { get; set; }
        public bool IsOwn { get; set; }
        public bool IsAnonymous { get; set; } = false;
        public string? DUI { get; set; }
        public string? ReferencedDUI { get; set; }

        public int ReadingTimeMinutes =>
            Math.Max(1, (int)Math.Ceiling(
                System.Text.RegularExpressions.Regex.Replace(Content ?? string.Empty, "<[^>]+>", " ")
                    .Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length / 200.0
            ));
    }

    public class ExploreListViewModel
    {
        public List<ExploreViewModel> PublicJournals { get; set; } = new List<ExploreViewModel>();
        public List<ExploreViewModel> FriendJournals { get; set; } = new List<ExploreViewModel>();
        public string? SearchQuery { get; set; }
        public string? TagQuery { get; set; }
        public string? SortBy { get; set; } = "recent";
    }
}
