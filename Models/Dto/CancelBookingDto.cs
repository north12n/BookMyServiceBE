using System.ComponentModel.DataAnnotations;

namespace BookMyServiceBE.Models.Dto
{
    public class CancelBookingDto
    {
        [MaxLength(200)]
        public string? Reason { get; set; }
    }

    public record CancelBookingResponse(
        int BookingId,
        string BookingCode,
        string Status,
        string? Reason,
        DateTime CancelledAt
    );
}
