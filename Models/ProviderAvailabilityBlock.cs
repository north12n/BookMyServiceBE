using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookMyService.Models
{
    public class ProviderAvailabilityBlock
    {
        public int BlockId { get; set; }

        [Required]
        public int ProviderId { get; set; }

        [Required]
        public DateTime StartUtc { get; set; }

        [Required]
        public DateTime EndUtc { get; set; }

        [MaxLength(200)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ProviderId))]
        public User Provider { get; set; } = null!;

    }
}
