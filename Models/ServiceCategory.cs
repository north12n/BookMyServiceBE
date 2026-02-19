using System.ComponentModel.DataAnnotations;

namespace BookMyService.Models
{
    public class ServiceCategory
    {
        public int ServiceCategoryId { get; set; }
        [MaxLength(100)] public string Name { get; set; } = null!;
        [MaxLength(500)] public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<ProviderService> ProviderServices { get; set; } = new List<ProviderService>();
    }
}
