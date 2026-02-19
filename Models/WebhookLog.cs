namespace BookMyService.Models
{
    public class WebhookLog
    {
        public int WebhookLogId { get; set; }
        public int? PaymentId { get; set; }
        public string RawPayload { get; set; } = null!;
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public Payment? Payment { get; set; }
    }
}
