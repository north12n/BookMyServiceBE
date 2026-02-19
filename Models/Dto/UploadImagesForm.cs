using System.ComponentModel.DataAnnotations;

namespace BookMyServiceBE.Models.Dto
{
    public class UploadImagesForm
    {
        [Required]
        public int ProviderId { get; set; }

        // ชื่อ field = files ให้ตรงกับใน Swagger form-data
        [Required]
        public List<IFormFile> Files { get; set; } = new();
    }
}
