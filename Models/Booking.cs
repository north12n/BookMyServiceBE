using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using BookMyServiceBE.Models;

namespace BookMyService.Models
{
    public class Booking
    {
        public int BookingId { get; set; }
        [MaxLength(50)] public string BookingCode { get; set; } = null!;

        public int UserId { get; set; }                  // customer
        public int ProviderServiceId { get; set; }

        [MaxLength(200)] public string JobTitle { get; set; } = null!;
        [MaxLength(1000)] public string? JobDescription { get; set; }

        public DateTime RequestedStartAt { get; set; }
        public DateTime? RequestedEndAt { get; set; }

        [MaxLength(300)] public string AddressLine { get; set; } = null!;
        [MaxLength(100)] public string District { get; set; } = null!;
        [MaxLength(100)] public string Province { get; set; } = null!;
        [MaxLength(10)] public string PostalCode { get; set; } = null!;

        [Column(TypeName = "decimal(10,2)")] public decimal EstimatedPrice { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal? FinalPrice { get; set; }
        // Cancel & Refund
        [MaxLength(500)] public string? CancelReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal? RefundAmount { get; set; }
        public int? RefundPercentage { get; set; }

        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigations
        public User Customer { get; set; } = null!;
        public ProviderService ProviderService { get; set; } = null!;

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public WorkLog? WorkLog { get; set; }
        public Review? Review { get; set; }
        public Payout? Payout { get; set; }
        public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
    }
}
