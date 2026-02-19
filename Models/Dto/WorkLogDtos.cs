using System.ComponentModel.DataAnnotations;

namespace BookMyServiceBE.Models.Dto
{
    public class CheckInRequest
    {
        [Required] public int BookingId { get; set; }
        [Required] public int ProviderId { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public string? Note { get; set; }
    }

    public class CheckOutRequest
    {
        [Required] public int BookingId { get; set; }
        [Required] public int ProviderId { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public string? Note { get; set; }
        public decimal? FinalPrice { get; set; } // why: ปิดงานพร้อมสรุปรายได้
    }

    public record WorkLogDto(
        int WorkLogId,
        int BookingId,
        int ProviderId,
        DateTime? CheckInTime,
        double? CheckInLat,
        double? CheckInLng,
        DateTime? CheckOutTime,
        double? CheckOutLat,
        double? CheckOutLng,
        string? Note,
        DateTime CreatedAt
    );
}
