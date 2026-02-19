namespace BookMyService.Models
{
    public class WorkLog
    {
        public int WorkLogId { get; set; }
        public int BookingId { get; set; } // unique (1:1)
        public int UserId { get; set; }    // provider

        public DateTime? CheckInTime { get; set; }
        public double? CheckInLat { get; set; }
        public double? CheckInLng { get; set; }

        public DateTime? CheckOutTime { get; set; }
        public double? CheckOutLat { get; set; }
        public double? CheckOutLng { get; set; }

        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Booking Booking { get; set; } = null!;
        public User Provider { get; set; } = null!;
    }

}
