using System.ComponentModel.DataAnnotations;

namespace BookMyServiceBE.Models.Dto
{
    public record PaymentListItemDto(
        int PaymentId,
        int BookingId,
        int UserId,
        decimal Amount,
        string Status,
        string? SlipPath,
        DateTime CreatedAt,
        DateTime? PaidAt
    );

    public record MarkPaidResponse(
        int PaymentId,
        string Status,
        DateTime PaidAt
    );

    public class RejectSlipDto
    {
        [Required] public string Reason { get; set; } = null!;
    }
}
