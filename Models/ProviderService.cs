// Models/ProviderService.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookMyService.Models
{
    public class ProviderService
    {
        public int ProviderServiceId { get; set; }

        public int ProviderId { get; set; }            // FK -> Users.UserId
        public int ServiceCategoryId { get; set; }     // FK -> ServiceCategories.ServiceCategoryId

        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal BasePrice { get; set; }

        [MaxLength(50)]
        public string? UnitLabel { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // NEW: เก็บรูปแบบ JSON ในคอลัมน์ nvarchar(max)
        public List<string>? Images { get; set; } = new();



        // Navigation
        public User Provider { get; set; } = null!;
        public ServiceCategory ServiceCategory { get; set; } = null!;
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
