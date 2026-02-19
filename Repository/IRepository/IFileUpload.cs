namespace BookMyServiceBE.Repository.IRepository
{
    public interface IFileUpload
    {
        Task<string> UploadFileAsync(IFormFile file, string subFolder = "");
        Task<List<string>> UploadFilesAsync(IEnumerable<IFormFile> files, string subFolder = "");

        // ✅ เพิ่ม 2 ตัวนี้ (สำหรับอัปโหลดบัตรประชาชน)
        Task<string> UploadKycFileAsync(IFormFile file, string subFolder = "");
        Task<List<string>> UploadKycFilesAsync(IEnumerable<IFormFile> files, string subFolder = "");

        bool DeleteFile(string publicPath);
    }
}
