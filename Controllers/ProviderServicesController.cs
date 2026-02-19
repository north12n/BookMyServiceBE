// File: Controllers/ProviderServicesController.cs
using BookMyService.Models;
using BookMyServiceBE.Models.Dto;
using BookMyServiceBE.Repository;
using BookMyServiceBE.Repository.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/services")]
    public class ProviderServicesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IRepository<ProviderService> _repo;
        private readonly IFileUpload _fileUpload;

        public ProviderServicesController(
            ApplicationDbContext db,
            IRepository<ProviderService> repo,
            IFileUpload fileUpload)
        {
            _db = db;
            _repo = repo;
            _fileUpload = fileUpload;
        }

        private static ObjectResult ForbidManual(string message) =>
            new ObjectResult(new { message }) { StatusCode = StatusCodes.Status403Forbidden };

        // =========================
        // 1) PUBLIC (ลูกค้าเห็น)
        // =========================

        // แนะนำให้ FE ฝั่งลูกค้าใช้ตัวนี้แทน all
        [HttpGet("public")]
        public async Task<ActionResult<IEnumerable<ProviderServiceResponse>>> GetPublic()
        {
            var items = await _db.ProviderServices
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Include(s => s.Provider)
                .Include(s => s.ServiceCategory)
                .Select(ProviderServiceResponseMap.Projection)
                .ToListAsync();

            return Ok(items);
        }

        // จะคง all ไว้ก็ได้ แต่ถ้าจะให้ปลอดภัย แนะนำให้ all = admin เท่านั้น
        // หรือจะลบทิ้งแล้วใช้ public + mine แทนก็ได้
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<ProviderServiceResponse>>> GetAll()
        {
            var items = await _db.ProviderServices
                .AsNoTracking()
                .Include(s => s.Provider)
                .Include(s => s.ServiceCategory)
                .Select(ProviderServiceResponseMap.Projection)
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProviderServiceResponse>> GetById([FromRoute] int id)
        {
            var result = await _db.ProviderServices.AsNoTracking()
                .Include(s => s.Provider)
                .Include(s => s.ServiceCategory)
                .Where(x => x.ProviderServiceId == id)
                .Select(ProviderServiceResponseMap.Projection)
                .FirstOrDefaultAsync();

            if (result is null) return NotFound();
            return Ok(result);
        }

        // =========================
        // 2) PROVIDER (ของฉัน)
        // =========================

        // Provider ดูบริการของตัวเองทั้งหมด (รวม inactive)
        [Authorize(Roles = "Provider")]
        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<ProviderServiceResponse>>> GetMine()
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var items = await _db.ProviderServices
                .AsNoTracking()
                .Where(s => s.ProviderId == providerId)
                .Include(s => s.Provider)
                .Include(s => s.ServiceCategory)
                .OrderByDescending(s => s.CreatedAt)
                .Select(ProviderServiceResponseMap.Projection)
                .ToListAsync();

            return Ok(items);
        }

        // =========================
        // 3) CREATE (Provider เท่านั้น)
        // =========================

        // ปรับ: ไม่รับ ProviderId จาก form แล้ว ใช้จาก token แทน
        [Authorize(Roles = "Provider")]
        [HttpPost("form")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ProviderServiceResponse>> CreateForm([FromForm] CreateProviderServiceFormDto dto)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var provider = await _db.Users.FirstOrDefaultAsync(u => u.UserId == providerId);
            if (provider is null) return Unauthorized();
            if (provider.UserRole != UserRole.Provider) return ForbidManual("User is not a provider.");

            var categoryExists = await _db.ServiceCategories.AnyAsync(c =>
                c.ServiceCategoryId == dto.ServiceCategoryId && c.IsActive);

            if (!categoryExists)
                return BadRequest(new { message = "ServiceCategoryId not found or inactive." });

            var title = dto.Title?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { message = "Title is required." });

            var dup = await _db.ProviderServices.AnyAsync(s => s.ProviderId == providerId && s.Title == title);
            if (dup) return Conflict(new { message = "Title already exists for this provider." });

            var entity = new ProviderService
            {
                ProviderId = providerId,
                ServiceCategoryId = dto.ServiceCategoryId,
                Title = title,
                Description = dto.Description?.Trim(),
                BasePrice = dto.BasePrice,
                UnitLabel = string.IsNullOrWhiteSpace(dto.UnitLabel) ? null : dto.UnitLabel.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Images = new List<string>()
            };

            await _repo.CreateAsync(entity); // ต้องให้ได้ ProviderServiceId ก่อน เพื่อทำ folder

            // อัปโหลดไฟล์ (ถ้ามี) -> เก็บไว้ที่ service-{providerServiceId}
            if (dto.Files is not null && dto.Files.Count > 0)
            {
                var paths = await _fileUpload.UploadFilesAsync(dto.Files, $"service-{entity.ProviderServiceId}");
                entity.Images = paths;
                await _repo.UpdateAsync(entity);
            }

            var result = await _db.ProviderServices.AsNoTracking()
                .Include(s => s.Provider).Include(s => s.ServiceCategory)
                .Where(x => x.ProviderServiceId == entity.ProviderServiceId)
                .Select(ProviderServiceResponseMap.Projection)
                .FirstAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.ProviderServiceId }, result);
        }

        // =========================
        // 4) UPDATE / ACTIVATE / DELETE (Provider owner เท่านั้น)
        // =========================

        [Authorize(Roles = "Provider")]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ProviderServiceResponse>> Update([FromRoute] int id, [FromBody] UpdateProviderServiceDto dto)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var entity = await _db.ProviderServices.FirstOrDefaultAsync(x => x.ProviderServiceId == id);
            if (entity is null) return NotFound();
            if (entity.ProviderId != providerId) return ForbidManual("You are not the owner of this service.");

            var categoryExists = await _db.ServiceCategories.AnyAsync(c =>
                c.ServiceCategoryId == dto.ServiceCategoryId && c.IsActive);

            if (!categoryExists)
                return BadRequest(new { message = "ServiceCategoryId not found or inactive." });

            var title = dto.Title?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { message = "Title is required." });

            var dup = await _db.ProviderServices.AnyAsync(s =>
                s.ProviderId == providerId &&
                s.Title == title &&
                s.ProviderServiceId != id);

            if (dup) return Conflict(new { message = "Title already exists for this provider." });

            entity.ServiceCategoryId = dto.ServiceCategoryId;
            entity.Title = title;
            entity.Description = dto.Description?.Trim();
            entity.BasePrice = dto.BasePrice;
            entity.UnitLabel = string.IsNullOrWhiteSpace(dto.UnitLabel) ? null : dto.UnitLabel.Trim();

            await _repo.UpdateAsync(entity);

            var result = await _db.ProviderServices.AsNoTracking()
                .Include(s => s.Provider).Include(s => s.ServiceCategory)
                .Where(x => x.ProviderServiceId == id)
                .Select(ProviderServiceResponseMap.Projection)
                .FirstAsync();

            return Ok(result);
        }

        [Authorize(Roles = "Provider")]
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> ToggleActive([FromRoute] int id, [FromBody] ToggleActiveDto dto)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var entity = await _db.ProviderServices.FirstOrDefaultAsync(x => x.ProviderServiceId == id);
            if (entity is null) return NotFound();
            if (entity.ProviderId != providerId) return ForbidManual("You are not the owner of this service.");

            entity.IsActive = dto.IsActive;
            await _repo.UpdateAsync(entity);
            return NoContent();
        }

        [Authorize(Roles = "Provider")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var entity = await _db.ProviderServices.FirstOrDefaultAsync(x => x.ProviderServiceId == id);
            if (entity is null) return NotFound();
            if (entity.ProviderId != providerId) return ForbidManual("You are not the owner of this service.");

            var hasBookings = await _db.Bookings.AnyAsync(b => b.ProviderServiceId == id);
            if (hasBookings)
            {
                if (entity.IsActive)
                {
                    entity.IsActive = false;
                    await _repo.UpdateAsync(entity);
                }
                return StatusCode(409, new { message = "Service is linked to bookings. Deactivated instead of deletion." });
            }

            if (entity.Images is not null)
            {
                foreach (var p in entity.Images)
                    _fileUpload.DeleteFile(p);
            }

            await _repo.RemoveAsync(entity);
            return NoContent();
        }

        // =========================
        // 5) IMAGES (Provider owner เท่านั้น)
        // =========================

        // ปรับ: ไม่รับ providerId จาก form แล้ว ใช้ token
        [Authorize(Roles = "Provider")]
        [HttpPost("{id:int}/images/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImages([FromRoute] int id, [FromForm] List<IFormFile> files)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var service = await _db.ProviderServices.FirstOrDefaultAsync(s => s.ProviderServiceId == id);
            if (service is null) return NotFound();
            if (service.ProviderId != providerId) return ForbidManual("You are not the owner of this service.");

            if (files is null || files.Count == 0)
                return BadRequest(new { message = "No files uploaded." });

            var paths = await _fileUpload.UploadFilesAsync(files, $"service-{id}");
            service.Images ??= new List<string>();
            service.Images.AddRange(paths.Distinct());

            await _repo.UpdateAsync(service);
            return Ok(service.Images);
        }

        // ปรับ: ไม่รับ ProviderId แล้ว ใช้ token
        [Authorize(Roles = "Provider")]
        [HttpDelete("{id:int}/images")]
        public async Task<IActionResult> DeleteImage([FromRoute] int id, [FromBody] DeleteImageDto dto)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var service = await _db.ProviderServices.FirstOrDefaultAsync(s => s.ProviderServiceId == id);
            if (service is null) return NotFound();
            if (service.ProviderId != providerId) return ForbidManual("You are not the owner of this service.");

            if (string.IsNullOrWhiteSpace(dto.Path))
                return BadRequest(new { message = "Path required." });

            if (service.Images is null || !service.Images.Remove(dto.Path))
                return NotFound(new { message = "Image not found in this service." });

            _fileUpload.DeleteFile(dto.Path);

            await _repo.UpdateAsync(service);
            return NoContent();
        }
    }
}
