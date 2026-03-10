using System.ComponentModel.DataAnnotations;
using InkVault.Models;

namespace InkVault.ViewModels
{
    public class CreateJournalViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = null!;

        /// <summary>
        /// Optional abstract/summary of the journal entry.
        /// Can be shown publicly while full content remains restricted.
        /// </summary>
        public string? Abstract { get; set; }

        /// <summary>
        /// Full journal content. Optional if abstract is provided.
        /// </summary>
        public string? Content { get; set; }

        [StringLength(50)]
        public string? Topic { get; set; }

        /// <summary>
        /// Comma-separated tags for the journal. Will be parsed and stored as JSON array.
        /// </summary>
        public string? Tags { get; set; }

        public JournalStatus Status { get; set; } = JournalStatus.Draft;

        public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.Private;

        /// <summary>
        /// If true, post anonymously. Anonymous journals must be public only.
        /// </summary>
        public bool IsAnonymous { get; set; } = false;

        /// <summary>
        /// Optional reference to another journal's DUI. Used when this journal is based on or inspired by another.
        /// </summary>
        [StringLength(20)]
        public string? ReferencedDUI { get; set; }

        /// <summary>
        /// Custom validation: At least one of Abstract or Content must be provided
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Abstract) && string.IsNullOrWhiteSpace(Content))
            {
                yield return new ValidationResult(
                    "Please enter content in either Abstract or Journal Content.",
                    new[] { nameof(Abstract), nameof(Content) }
                );
            }
        }
    }

    public class EditJournalViewModel : IValidatableObject
    {
        public int JournalId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = null!;

        /// <summary>
        /// Optional abstract/summary of the journal entry.
        /// </summary>
        public string? Abstract { get; set; }

        /// <summary>
        /// Full journal content. Optional if abstract is provided.
        /// </summary>
        public string? Content { get; set; }

        [StringLength(50)]
        public string? Topic { get; set; }

        /// <summary>
        /// Comma-separated tags for the journal. Will be parsed and stored as JSON array.
        /// </summary>
        public string? Tags { get; set; }

        public JournalStatus Status { get; set; } = JournalStatus.Draft;

        public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.Private;

        /// <summary>
        /// If true, post anonymously. Anonymous journals must be public only.
        /// </summary>
        public bool IsAnonymous { get; set; } = false;

        /// <summary>
        /// Custom validation: At least one of Abstract or Content must be provided
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Abstract) && string.IsNullOrWhiteSpace(Content))
            {
                yield return new ValidationResult(
                    "Please enter content in either Abstract or Journal Content.",
                    new[] { nameof(Abstract), nameof(Content) }
                );
            }
        }
    }

    public class JournalListViewModel
    {
        public int JournalId { get; set; }
        public string Title { get; set; } = null!;
        public string? Abstract { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public JournalStatus Status { get; set; }
        public PrivacyLevel PrivacyLevel { get; set; }
        public string? Topic { get; set; }
        public List<string>? Tags { get; set; }
        public int ViewCount { get; set; }
        public bool IsAnonymous { get; set; } = false;
        public string? DUI { get; set; }
        public string? ReferencedDUI { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsPinned { get; set; }

        // Set by the controller using the full (untruncated) content
        public int ReadingTimeMinutes { get; set; } = 1;
    }
}
