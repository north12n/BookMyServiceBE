using BookMyService.Models;
using BookMyServiceBE.Repository;
using BookMyServiceBE.Repository.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/account")]
    [Produces("application/json")]
    public sealed class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileUpload _fileUpload;
        private readonly IJwtTokenService _jwt;

        public AccountController(ApplicationDbContext db, IFileUpload fileUpload, IJwtTokenService jwt)
        {
            _db = db;
            _fileUpload = fileUpload;
            _jwt = jwt;
        }

        public sealed class UpgradeToProviderForm
        {
            public string BankName { get; set; } = null!;
            public string BankAccountNumber { get; set; } = null!;
            public string BankAccountName { get; set; } = null!;
            public List<IFormFile> IdCardFiles { get; set; } = new(); // ต้อง 2 รูป
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("upgrade/provider")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpgradeToProvider([FromForm] UpgradeToProviderForm form)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user is null) return Unauthorized();

            if (user.UserRole == UserRole.Provider)
                return Conflict(new { message = "Already provider." });

            if (string.IsNullOrWhiteSpace(form.BankName))
                return BadRequest(new { message = "BankName is required." });

            if (string.IsNullOrWhiteSpace(form.BankAccountNumber))
                return BadRequest(new { message = "BankAccountNumber is required." });

            if (string.IsNullOrWhiteSpace(form.BankAccountName))
                return BadRequest(new { message = "BankAccountName is required." });

            if (form.IdCardFiles is null || form.IdCardFiles.Count != 2)
                return BadRequest(new { message = "IdCardFiles must contain exactly 2 images (front & back)." });

            // upload to /uploads/kyc/user-<id>/
            // หมายเหตุ: ต้องมีเมธอด UploadKycFilesAsync ใน IFileUpload ด้วย (ดูหมายเหตุท้ายข้อความ)
            var paths = await _fileUpload.UploadKycFilesAsync(form.IdCardFiles, $"user-{userId}");

            user.BankName = form.BankName.Trim();
            user.BankAccountNumber = form.BankAccountNumber.Trim();
            user.BankAccountName = form.BankAccountName.Trim();
            user.IdCardFrontPath = paths[0];
            user.IdCardBackPath = paths[1];

            user.UserRole = UserRole.Provider;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // ✅ ออก token ใหม่ทันที เพราะ role เปลี่ยนแล้ว
            var token = _jwt.CreateToken(user);

            return Ok(new
            {
                message = "Upgraded to provider.",
                token,
                user = new
                {
                    user.UserId,
                    user.Email,
                    user.FullName,
                    user.PhoneNumber,
                    Role = user.UserRole.ToString(),
                    user.BankName,
                    user.BankAccountNumber,
                    user.BankAccountName,
                    user.IdCardFrontPath,
                    user.IdCardBackPath
                }
            });
        }
    }
}
