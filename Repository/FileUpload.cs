// File: Repository/FileUpload.cs
using BookMyServiceBE.Repository.IRepository;
using BookMyServiceBE.Utility;

namespace BookMyServiceBE.Repository
{
    public class FileUpload : IFileUpload
    {
        private readonly IWebHostEnvironment _env;

        public FileUpload(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string EnsureWebRoot()
        {
            var webRoot = _env.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
                Directory.CreateDirectory(webRoot);
                _env.WebRootPath = webRoot;
            }

            return webRoot;
        }

        private static string NormalizeSubFolder(string subFolder)
            => string.IsNullOrWhiteSpace(subFolder) ? "" : subFolder.Trim().Trim('/');

        private string GetPhysicalFolder(string baseRel, string subFolder)
        {
            var basePath = string.IsNullOrWhiteSpace(baseRel) ? "/uploads" : baseRel;

            var rel = string.IsNullOrWhiteSpace(subFolder)
                ? basePath.Trim('/')
                : $"{basePath.Trim('/')}/{NormalizeSubFolder(subFolder)}";

            var webRoot = EnsureWebRoot();

            var relOs = rel.Replace("/", Path.DirectorySeparatorChar.ToString());
            var physical = Path.Combine(webRoot, relOs);

            Directory.CreateDirectory(physical);
            return physical;
        }

        private static void ValidateImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file uploaded");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!allowed.Contains(ext))
                throw new InvalidOperationException("Invalid file type");
        }

        private async Task<string> UploadInternalAsync(IFormFile file, string baseRel, string subFolder)
        {
            ValidateImage(file);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var physicalFolder = GetPhysicalFolder(baseRel, subFolder);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(physicalFolder, fileName);

            await using var fs = new FileStream(physicalPath, FileMode.Create);
            await file.CopyToAsync(fs);

            var relative = string.IsNullOrWhiteSpace(subFolder)
                ? $"{baseRel}/{fileName}"
                : $"{baseRel}/{NormalizeSubFolder(subFolder)}/{fileName}";

            return relative.Replace("//", "/");
        }

        /// <summary>
        /// Upload service image(s) -> base path = SD.ServiceImgPath (default: /uploads/services)
        /// </summary>
        public Task<string> UploadFileAsync(IFormFile file, string subFolder = "")
        {
            var baseRel = string.IsNullOrWhiteSpace(SD.ServiceImgPath) ? "/uploads/services" : SD.ServiceImgPath;
            return UploadInternalAsync(file, baseRel, subFolder);
        }

        /// <summary>
        /// Upload many service images -> base path = SD.ServiceImgPath
        /// </summary>
        public async Task<List<string>> UploadFilesAsync(IEnumerable<IFormFile> files, string subFolder = "")
        {
            var results = new List<string>();
            if (files == null) return results;

            foreach (var f in files)
                results.Add(await UploadFileAsync(f, subFolder));

            return results;
        }

        /// <summary>
        /// (Optional) Upload KYC image(s) -> base path = SD.KycImgPath (default: /uploads/kyc)
        /// NOTE: ไม่อยู่ใน IFileUpload; เรียกผ่าน concrete FileUpload ได้
        /// </summary>
        //public Task<string> UploadKycFileAsync(IFormFile file, string subFolder = "")
        //{
        //    var baseRel = (typeof(SD).GetField("KycImgPath")?.GetValue(null) as string) ?? "/uploads/kyc";
        //    if (string.IsNullOrWhiteSpace(baseRel)) baseRel = "/uploads/kyc";
        //    return UploadInternalAsync(file, baseRel, subFolder);
        //}
        public Task<string> UploadKycFileAsync(IFormFile file, string subFolder = "")
        {
            var baseRel = string.IsNullOrWhiteSpace(SD.KycImgPath) ? "/uploads/kyc" : SD.KycImgPath;
            return UploadInternalAsync(file, baseRel, subFolder);
        }


        /// <summary>
        /// (Optional) Upload many KYC images -> base path = SD.KycImgPath
        /// </summary>
        public async Task<List<string>> UploadKycFilesAsync(IEnumerable<IFormFile> files, string subFolder = "")
        {
            var results = new List<string>();
            if (files == null) return results;

            foreach (var f in files)
                results.Add(await UploadKycFileAsync(f, subFolder));

            return results;
        }

        public bool DeleteFile(string publicPath)
        {
            if (string.IsNullOrWhiteSpace(publicPath)) return false;

            var webRoot = EnsureWebRoot();
            var rel = publicPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
            var physical = Path.Combine(webRoot, rel);

            if (!File.Exists(physical)) return false; 

            File.Delete(physical);
            return true;
        }
    }
}
