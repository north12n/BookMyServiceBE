using System.ComponentModel.DataAnnotations;

namespace BookMyServiceBE.Models.Dto
{
    public class CreateReviewDto
    {
        [Required]
        public int BookingId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

    public record ReviewResponseDto(
        int ReviewId,
        int BookingId,
        int Rating,
        string? Comment,
        string ReviewerName,
        DateTime CreatedAt
    );


    public sealed record MyReviewItemDto(
        int ReviewId,
        int BookingId,
        int Rating,
        string? Comment,
        DateTime CreatedAt,
        string ServiceTitle,
        int ProviderId,
        string ProviderName
    );
    public sealed class UpdateReviewDto
    {
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

}
