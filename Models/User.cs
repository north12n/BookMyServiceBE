// Models/User.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookMyService.Models
{
    public class User
    {
        public int UserId { get; set; }

        [MaxLength(256)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        public string PasswordHash { get; set; } = null!;

        [Required, MaxLength(200)]
        public string FullName { get; set; } = null!;

        [Required]
        public UserRole UserRole { get; set; }

        [MaxLength(300)]
        public string? AddressLine { get; set; }

        [MaxLength(100)]
        public string? District { get; set; }

        [MaxLength(100)]
        public string? Province { get; set; }

        [MaxLength(10)]
        public string? PostalCode { get; set; }

        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }
        // ✅ NEW: ชื่อบัญชีธนาคาร (ใน requirement ของคุณมี)
        [MaxLength(200)]
        public string? BankAccountName { get; set; }

        // ✅ NEW: รูปยืนยันตัวตน (หน้า/หลัง)
        [MaxLength(400)]
        public string? IdCardFrontPath { get; set; }

        [MaxLength(400)]
        public string? IdCardBackPath { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [MaxLength(20)]
        public string? PromptPayId { get; set; } // เบอร์ 10 หลัก หรือเลขบัตร 13 หลัก


        // Navigations
        public ICollection<ProviderService> ProviderServices { get; set; } = new List<ProviderService>();
        public ICollection<ProviderAvailabilityBlock> AvailabilityBlocks { get; set; } = new List<ProviderAvailabilityBlock>();


        // ป้องกัน EF สร้าง mapping ที่ไม่ตรง schema ปัจจุบัน
        [NotMapped] public ICollection<Booking> CustomerBookings { get; set; } = new List<Booking>();
        [NotMapped] public ICollection<Booking> ProviderBookings { get; set; } = new List<Booking>();
    }
}
