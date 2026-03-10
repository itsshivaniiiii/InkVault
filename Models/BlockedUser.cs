using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkVault.Models
{
    public class BlockedUser
    {
        [Key]
        public int BlockedUserId { get; set; }

        [Required]
        public string BlockerId { get; set; } = null!;

        [Required]
        public string BlockedId { get; set; } = null!;

        [ForeignKey("BlockerId")]
        public ApplicationUser? Blocker { get; set; }

        [ForeignKey("BlockedId")]
        public ApplicationUser? Blocked { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
