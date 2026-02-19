using BookMyService.Models;
using BookMyServiceBE.Models;
using BookMyServiceBE.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/complaints")]
    public class ComplaintsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ComplaintsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // DTO for responses
        public class ComplaintDto
        {
            public int ComplaintId { get; set; }
            public int BookingId { get; set; }
            public int UserId { get; set; }
            public string Title { get; set; } = null!;
            public string? Description { get; set; }
            public string Status { get; set; } = null!;
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }

            public BookingInfoDto? Booking { get; set; }
            public UserInfoDto? Creator { get; set; }
            public int EvidenceCount { get; set; }

            public class BookingInfoDto
            {
                public string BookingCode { get; set; } = null!;
                public ServiceInfoDto Service { get; set; } = null!;

                public class ServiceInfoDto
                {
                    public string Title { get; set; } = null!;
                }
            }

            public class UserInfoDto
            {
                public int UserId { get; set; }
                public string FullName { get; set; } = null!;
                public string? Email { get; set; }
            }
        }

        // DTO for create
        public class CreateComplaintDto
        {
            public int BookingId { get; set; }
            public string Title { get; set; } = null!;
            public string? Description { get; set; }
        }

        // DTO for update status
        public class UpdateComplaintStatusDto
        {
            public string Status { get; set; } = null!;
            public string? Notes { get; set; }
        }

        // DTO for close
        public class CloseComplaintDto
        {
            public string Resolution { get; set; } = null!;
        }

        // =========================
        // GET /api/complaints - Get all complaints (with optional filters)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<ComplaintDto>>> GetAll([FromQuery] string? status)
        {
            var query = _db.Complaints
                .Include(c => c.Booking)
                    .ThenInclude(b => b.ProviderService)
                .Include(c => c.Creator)
                .AsQueryable();

            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<ComplaintStatus>(status, true, out var statusEnum))
                {
                    query = query.Where(c => c.Status == statusEnum);
                }
            }

            var complaints = await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new ComplaintDto
                {
                    ComplaintId = c.ComplaintId,
                    BookingId = c.BookingId,
                    UserId = c.UserId,
                    Title = c.Title,
                    Description = c.Description,
                    Status = c.Status.ToString(),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    Booking = new ComplaintDto.BookingInfoDto
                    {
                        BookingCode = c.Booking.BookingCode,
                        Service = new ComplaintDto.BookingInfoDto.ServiceInfoDto
                        {
                            Title = c.Booking.ProviderService.Title
                        }
                    },
                    Creator = new ComplaintDto.UserInfoDto
                    {
                        UserId = c.Creator.UserId,
                        FullName = c.Creator.FullName,
                        Email = c.Creator.Email
                    },
                    EvidenceCount = 0 // TODO: Add evidence tracking if needed
                })
                .ToListAsync();

            return Ok(complaints);
        }

        // =========================
        // GET /api/complaints/{id} - Get complaint by ID
        // =========================
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<ActionResult<ComplaintDto>> GetById([FromRoute] int id)
        {
            var complaint = await _db.Complaints
                .Include(c => c.Booking)
                    .ThenInclude(b => b.ProviderService)
                .Include(c => c.Creator)
                .FirstOrDefaultAsync(c => c.ComplaintId == id);

            if (complaint is null)
                return NotFound(new { message = "Complaint not found." });

            var userId = User.GetUserId();
            var isAdmin = User.IsInRole("Admin");

            // Only admin or the creator can view
            if (!isAdmin && complaint.UserId != userId)
                return Forbid();

            var dto = new ComplaintDto
            {
                ComplaintId = complaint.ComplaintId,
                BookingId = complaint.BookingId,
                UserId = complaint.UserId,
                Title = complaint.Title,
                Description = complaint.Description,
                Status = complaint.Status.ToString(),
                CreatedAt = complaint.CreatedAt,
                UpdatedAt = complaint.UpdatedAt,
                Booking = new ComplaintDto.BookingInfoDto
                {
                    BookingCode = complaint.Booking.BookingCode,
                    Service = new ComplaintDto.BookingInfoDto.ServiceInfoDto
                    {
                        Title = complaint.Booking.ProviderService.Title
                    }
                },
                Creator = new ComplaintDto.UserInfoDto
                {
                    UserId = complaint.Creator.UserId,
                    FullName = complaint.Creator.FullName,
                    Email = complaint.Creator.Email
                },
                EvidenceCount = 0
            };

            return Ok(dto);
        }

        // =========================
        // POST /api/complaints - Create new complaint
        // =========================
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ComplaintDto>> Create([FromBody] CreateComplaintDto dto)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            // Validate booking exists
            var booking = await _db.Bookings
                .Include(b => b.ProviderService)
                .FirstOrDefaultAsync(b => b.BookingId == dto.BookingId);

            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            // Check if user is related to this booking (customer or provider)
            var isCustomer = booking.UserId == userId;
            var isProvider = booking.ProviderService.ProviderId == userId;

            if (!isCustomer && !isProvider)
                return Forbid();

            var complaint = new Complaint
            {
                BookingId = dto.BookingId,
                UserId = userId,
                Title = dto.Title,
                Description = dto.Description,
                Status = ComplaintStatus.Open,
                CreatedAt = DateTime.UtcNow
            };

            _db.Complaints.Add(complaint);
            await _db.SaveChangesAsync();

            // Reload with relations
            await _db.Entry(complaint)
                .Reference(c => c.Creator)
                .LoadAsync();
            await _db.Entry(complaint)
                .Reference(c => c.Booking)
                .LoadAsync();
            await _db.Entry(complaint.Booking)
                .Reference(b => b.ProviderService)
                .LoadAsync();

            var result = new ComplaintDto
            {
                ComplaintId = complaint.ComplaintId,
                BookingId = complaint.BookingId,
                UserId = complaint.UserId,
                Title = complaint.Title,
                Description = complaint.Description,
                Status = complaint.Status.ToString(),
                CreatedAt = complaint.CreatedAt,
                UpdatedAt = complaint.UpdatedAt,
                Booking = new ComplaintDto.BookingInfoDto
                {
                    BookingCode = complaint.Booking.BookingCode,
                    Service = new ComplaintDto.BookingInfoDto.ServiceInfoDto
                    {
                        Title = complaint.Booking.ProviderService.Title
                    }
                },
                Creator = new ComplaintDto.UserInfoDto
                {
                    UserId = complaint.Creator.UserId,
                    FullName = complaint.Creator.FullName,
                    Email = complaint.Creator.Email
                },
                EvidenceCount = 0
            };

            return CreatedAtAction(nameof(GetById), new { id = complaint.ComplaintId }, result);
        }

        // =========================
        // PATCH /api/complaints/{id}/status - Update complaint status
        // =========================
        [HttpPatch("{id:int}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus([FromRoute] int id, [FromBody] UpdateComplaintStatusDto dto)
        {
            var complaint = await _db.Complaints.FindAsync(id);
            if (complaint is null)
                return NotFound(new { message = "Complaint not found." });

            if (!Enum.TryParse<ComplaintStatus>(dto.Status, true, out var newStatus))
                return BadRequest(new { message = "Invalid status." });

            complaint.Status = newStatus;
            complaint.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Status updated successfully." });
        }

        // =========================
        // POST /api/complaints/{id}/close - Close complaint
        // =========================
        [HttpPost("{id:int}/close")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Close([FromRoute] int id, [FromBody] CloseComplaintDto dto)
        {
            var complaint = await _db.Complaints.FindAsync(id);
            if (complaint is null)
                return NotFound(new { message = "Complaint not found." });

            complaint.Status = ComplaintStatus.Closed;
            complaint.UpdatedAt = DateTime.UtcNow;
            // You can store resolution in Description or add a new field
            complaint.Description = (complaint.Description ?? "") + "\n\n[Resolution]: " + dto.Resolution;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Complaint closed successfully." });
        }
    }
}
