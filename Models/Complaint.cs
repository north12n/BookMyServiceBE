using BookMyService.Models;
using System.ComponentModel.DataAnnotations;

namespace BookMyServiceBE.Models
{
    public class Complaint
    {
        public int ComplaintId { get; set; }
        public int BookingId { get; set; }
        public int UserId { get; set; } // creator

        [MaxLength(200)]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }
        public ComplaintStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Booking Booking { get; set; } = null!;
        public User Creator { get; set; } = null!;
    }
}
