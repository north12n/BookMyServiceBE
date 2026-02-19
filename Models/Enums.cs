namespace BookMyService.Models
{
    public enum UserRole { Customer = 1, Provider = 2, Admin = 3 }

    // NOTE:
    // - คงเลขเดิม: InProgress=3, Completed=4, Cancelled=5 (กันข้อมูลเก่าเพี้ยน)
    // - เพิ่ม Assigned/CancelledByCustomer/CancelledByProvider ต่อท้าย
    // - ถ้าจะเลิกใช้ Cancelled เดิม ให้ [Obsolete] ไว้ (คอมเมนต์บรรทัดล่าง)
    public enum BookingStatus
    {
        PendingPayment = 1,
        Paid = 2,
        InProgress = 3,
        Completed = 4,
        Cancelled = 5, 
        Assigned = 6,
        CancelledByCustomer = 7,
        CancelledByProvider = 8
    }

    public enum PaymentType { Deposit = 1, Full = 2, Extra = 3 }
    public enum PaymentMethod { PromptPay = 1, OPN = 2 }
    public enum ComplaintStatus
    {
        Open = 1,
        Investigating = 2,
        Resolved = 3,
        Closed = 4,
        Rejected = 5
    }
    public enum SenderRole { User = 1, AI = 2 }
    public enum PaymentStatus
    {
        Pending = 1,          // สร้างแล้ว ยังไม่อัปสลิป
        SlipUploaded = 2,     // ลูกค้าอัปสลิปแล้ว รอแอดมินตรวจ
        Paid = 3,             // แอดมินยืนยันแล้ว
        Rejected = 4,         // (optional) ปฏิเสธสลิป
        Refunded = 5          // (optional) คืนเงิน
    }

}
