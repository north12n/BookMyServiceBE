using System.ComponentModel.DataAnnotations;

namespace BookMyService.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        public int BookingId { get; set; } // unique (1:1)
        public int UserId { get; set; }    // reviewer (customer)

        public int Rating { get; set; }    // 1..5

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Booking Booking { get; set; } = null!;
        public User Reviewer { get; set; } = null!;
    }

}
