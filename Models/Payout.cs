using BookMyService.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookMyServiceBE.Models
{
    public class Payout
    {
        public int PayoutId { get; set; }
        public int UserId { get; set; }        // provider
        public int BookingId { get; set; }     // unique (1:1)

        [Column(TypeName = "decimal(10,2)")] public decimal GrossAmount { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal FeeAmount { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal NetAmount { get; set; }

        public DateTime? SettledAt { get; set; }
        public int Status { get; set; }        // 1=Pending, 2=Paid
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? TransactionRef { get; set; }

        
        public string? TransferSlipPath { get; set; }

        public User Provider { get; set; } = null!;
        public Booking Booking { get; set; } = null!;
    }

}
