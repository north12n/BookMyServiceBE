namespace BookMyServiceBE.Models.Dto
{
    public class RemoveImagesDto
    {
        public int ProviderId { get; set; }                // ใช้ยืนยันเจ้าของ
        public List<string>? ImageUrls { get; set; }       // รูปที่ต้องการลบเฉพาะรายการ
        public bool RemoveAll { get; set; } = false;       // true = ลบทุกรูป
    }
}
