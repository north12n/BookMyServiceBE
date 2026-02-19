// File: Models/Dto/ProviderServiceFormDtos.cs
using System.ComponentModel.DataAnnotations;

namespace BookMyServiceBE.Models.Dto
{

    public class CreateProviderServiceFormDto
    {
        [Required] public int ServiceCategoryId { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BasePrice { get; set; }

        [MaxLength(50)]
        public string? UnitLabel { get; set; }

        public List<IFormFile>? Files { get; set; }
    }

    // ใช้กับ DELETE /api/services/{id}/images?path=...
    public class DeleteImageDto
    {
        [Required] public string Path { get; set; } = null!;
    }
}
