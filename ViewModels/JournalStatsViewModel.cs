using InkVault.Models;

namespace InkVault.ViewModels
{
    public class JournalStatItemViewModel
    {
        public int JournalId { get; set; }
        public string Title { get; set; } = null!;
        public string? Topic { get; set; }
        public DateTime CreatedAt { get; set; }
        public PrivacyLevel PrivacyLevel { get; set; }
        public JournalStatus Status { get; set; }
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public string? DUI { get; set; }
        public string Content { get; set; } = string.Empty;

        public int ReadingTimeMinutes =>
            Math.Max(1, (int)Math.Ceiling(
                System.Text.RegularExpressions.Regex.Replace(Content ?? string.Empty, "<[^>]+>", " ")
                    .Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length / 200.0
            ));
    }
}
