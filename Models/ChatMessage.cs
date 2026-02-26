namespace BookMyService.Models
{
    public class ChatMessage
    {
        public int ChatMessageId { get; set; }
        public int UserId { get; set; }               // sender
        public int? RecipientUserId { get; set; }     // direct chat recipient
        public int? RelatedBookingId { get; set; }
        public SenderRole SenderRole { get; set; }
        public string MessageText { get; set; } = null!;
        public string? AttachmentPath { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentMimeType { get; set; }
        public long? AttachmentSize { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User Sender { get; set; } = null!;
        public User? Recipient { get; set; }
        public Booking? Booking { get; set; }
    }
}
