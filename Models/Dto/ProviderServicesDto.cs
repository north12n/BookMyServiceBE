using BookMyService.Models;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;


namespace BookMyServiceBE.Models.Dto
{
    public record ProviderServiceQuery(
      int? ProviderId = null,
      int? CategoryId = null,
      string? Q = null,
      bool? IsActive = true,
      int Page = 1,
      int PageSize = 20)
    { 
        public int PageSize { get; init; } = Math.Clamp(PageSize, 1, 100);
        public int Page { get; init; } = Math.Max(Page, 1);
    }

    public class CreateProviderServiceDto
    {
        [Required]
        public int ServiceCategoryId { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BasePrice { get; set; }

        [MaxLength(50)]
        public string? UnitLabel { get; set; }

        // เผื่อกรณี client ส่ง URL ที่มีอยู่แล้ว (optional)
        public List<string>? ImageUrls { get; set; }
    }


    public class UpdateProviderServiceDto
    {
        [Required]
        public int ServiceCategoryId { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BasePrice { get; set; }

        [MaxLength(50)]
        public string? UnitLabel { get; set; }

        public List<string>? ImageUrls { get; set; }
    }

    //public class ReplaceImagesDto
    //{
    //    [Required] public int ProviderId { get; set; }
    //    [Required] public List<string> ImageUrls { get; set; } = new();
    //}
    //public class ToggleActiveDto
    //{
    //    [Required] public int ProviderId { get; set; } // ใช้ยืนยันเจ้าของ
    //    [Required] public bool IsActive { get; set; }
    //}
    public class ReplaceImagesDto
    {
        [Required] public List<string> ImageUrls { get; set; } = new();
    }

    public class ToggleActiveDto
    {
        [Required] public bool IsActive { get; set; }
    }



}
