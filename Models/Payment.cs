using System.ComponentModel.DataAnnotations.Schema;

namespace BookMyService.Models
{

    public class Payment
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }
        public int UserId { get; set; }    // payer

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public PaymentType PaymentType { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus Status { get; set; }
        public string? TransactionRef { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? SlipPath { get; set; }
        public Booking Booking { get; set; } = null!;
        public User Payer { get; set; } = null!;
        public ICollection<WebhookLog> WebhookLogs { get; set; } = new List<WebhookLog>();  
    }

}
