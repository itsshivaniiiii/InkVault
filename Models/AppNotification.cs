using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkVault.Models
{
    public class AppNotification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RecipientId { get; set; } = null!;

        [ForeignKey("RecipientId")]
        public ApplicationUser Recipient { get; set; } = null!;

        public string? ActorId { get; set; }

        [ForeignKey("ActorId")]
        public ApplicationUser? Actor { get; set; }

        [Required]
        public NotificationType Type { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = null!;

        public string? ResourceUrl { get; set; }

        public int? JournalId { get; set; }

        [StringLength(200)]
        public string? JournalTitle { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum NotificationType
    {
        LikeReceived            = 1,
        LikeRemoved             = 2,
        CommentReceived         = 3,
        CommentDeleted          = 4,
        FriendRequestReceived   = 5,
        FriendRequestAccepted   = 6,
        FriendRequestDeclined   = 7,
        FriendRequestWithdrawn  = 8,
        FriendRemoved           = 9,
        JournalReferenced       = 10,
        JournalPublished        = 11,
        FullTextRequested       = 12
    }
}
