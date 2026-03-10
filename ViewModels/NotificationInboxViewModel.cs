using InkVault.Models;

namespace InkVault.ViewModels
{
    public class NotificationInboxViewModel
    {
        public List<NotificationItemViewModel> AllNotifications { get; set; } = new();
        public int UnreadCount { get; set; }

        public IEnumerable<NotificationItemViewModel> LikesAndComments =>
            AllNotifications.Where(n => n.Section is "likes" or "comments");

        public IEnumerable<NotificationItemViewModel> FriendNotifications =>
            AllNotifications.Where(n => n.Section == "friends");

        public IEnumerable<NotificationItemViewModel> JournalNotifications =>
            AllNotifications.Where(n => n.Section == "journals");
    }

    public class NotificationItemViewModel
    {
        public int Id { get; set; }
        public NotificationType Type { get; set; }
        public string Message { get; set; } = null!;
        public string? ResourceUrl { get; set; }
        public string? ActorId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorProfilePicture { get; set; }
        public int? JournalId { get; set; }
        public string? JournalTitle { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public string Icon => Type switch
        {
            NotificationType.LikeReceived           => "bi-heart-fill",
            NotificationType.LikeRemoved            => "bi-heart",
            NotificationType.CommentReceived        => "bi-chat-dots-fill",
            NotificationType.CommentDeleted         => "bi-chat-x",
            NotificationType.FriendRequestReceived  => "bi-person-plus-fill",
            NotificationType.FriendRequestAccepted  => "bi-person-check-fill",
            NotificationType.FriendRequestDeclined  => "bi-person-x-fill",
            NotificationType.FriendRequestWithdrawn => "bi-person-dash",
            NotificationType.FriendRemoved          => "bi-person-x",
            NotificationType.JournalReferenced      => "bi-quote",
            NotificationType.JournalPublished       => "bi-journal-text",
            NotificationType.FullTextRequested      => "bi-file-text",
            _                                       => "bi-bell"
        };

        public string IconColor => Type switch
        {
            NotificationType.LikeReceived           => "#e91e63",
            NotificationType.LikeRemoved            => "#9e9e9e",
            NotificationType.CommentReceived        => "#667eea",
            NotificationType.CommentDeleted         => "#9e9e9e",
            NotificationType.FriendRequestReceived  => "#4caf50",
            NotificationType.FriendRequestAccepted  => "#4caf50",
            NotificationType.FriendRequestDeclined  => "#f44336",
            NotificationType.FriendRequestWithdrawn => "#ff9800",
            NotificationType.FriendRemoved          => "#f44336",
            NotificationType.JournalReferenced      => "#764ba2",
            NotificationType.JournalPublished       => "#667eea",
            NotificationType.FullTextRequested      => "#ff9800",
            _                                       => "#667eea"
        };

        public string Section => Type switch
        {
            NotificationType.LikeReceived or NotificationType.LikeRemoved                                              => "likes",
            NotificationType.CommentReceived or NotificationType.CommentDeleted                                         => "comments",
            NotificationType.FriendRequestReceived or NotificationType.FriendRequestAccepted
                or NotificationType.FriendRequestDeclined or NotificationType.FriendRequestWithdrawn
                or NotificationType.FriendRemoved                                                                       => "friends",
            NotificationType.JournalReferenced or NotificationType.JournalPublished or NotificationType.FullTextRequested => "journals",
            _                                                                                                           => "other"
        };
    }
}
