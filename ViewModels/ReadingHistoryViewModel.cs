using InkVault.Models;

namespace InkVault.ViewModels
{
    public class ReadingHistoryItemViewModel
    {
        public int JournalId { get; set; }
        public string Title { get; set; } = null!;
        public string? AuthorName { get; set; }
        public string? AuthorUserId { get; set; }
        public string? Topic { get; set; }
        public DateTime ViewedAt { get; set; }
        public bool IsAnonymous { get; set; }
        public bool IsOwn { get; set; }
        public PrivacyLevel PrivacyLevel { get; set; }
        public string Content { get; set; } = string.Empty;

        public int ReadingTimeMinutes =>
            Math.Max(1, (int)Math.Ceiling(
                System.Text.RegularExpressions.Regex.Replace(Content ?? string.Empty, "<[^>]+>", " ")
                    .Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length / 200.0
            ));
    }
}
