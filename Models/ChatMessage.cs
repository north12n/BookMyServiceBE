namespace BookMyService.Models
{
    public class ChatMessage
    {
        public int ChatMessageId { get; set; }
        public int UserId { get; set; }               // sender
        public int? RelatedBookingId { get; set; }
        public SenderRole SenderRole { get; set; }
        public string MessageText { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User Sender { get; set; } = null!;
        public Booking? Booking { get; set; }
    }
}
