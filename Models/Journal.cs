using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkVault.Models
{
    public class Journal
    {
        [Key]
        public int JournalId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = null!;

        /// <summary>
        /// Optional abstract/summary of the journal entry.
        /// Can be shown publicly while full content remains restricted.
        /// </summary>
        public string? Abstract { get; set; }

        /// <summary>
        /// Full journal content. Can be empty if only abstract is provided.
        /// </summary>
        public string? Content { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public JournalStatus Status { get; set; } = JournalStatus.Draft;

        [Required]
        public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.Private;

        public string? Topic { get; set; }

        /// <summary>
        /// JSON array of tags associated with this journal. 
        /// Stored as JSON to support multiple tags efficiently.
        /// </summary>
        public string? Tags { get; set; }

        public int ViewCount { get; set; } = 0;

        /// <summary>
        /// If true, the author's identity is hidden. Anonymous journals must always be Public.
        /// </summary>
        public bool IsAnonymous { get; set; } = false;

        /// <summary>
        /// Document Unique Identifier - A unique identifier assigned after journal is posted.
        /// Format: JRN-{number} (e.g., JRN-10234)
        /// </summary>
        [StringLength(20)]
        public string? DUI { get; set; }

        /// <summary>
        /// Optional reference to another journal's DUI. Used when this journal is based on or inspired by another.
        /// </summary>
        [StringLength(20)]
        public string? ReferencedDUI { get; set; }

        public bool IsPinned { get; set; } = false;

        // Navigation properties
        public ICollection<JournalView> Views { get; set; } = new List<JournalView>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }

    public enum JournalStatus
    {
        Draft = 0,
        Published = 1,
        Archived = 2
    }

    public enum PrivacyLevel
    {
        Private = 0,
        FriendsOnly = 1,
        Public = 2
    }
}
