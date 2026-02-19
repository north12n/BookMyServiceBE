using BookMyService.Models;
using BookMyServiceBE.Models.Dto;
using BookMyServiceBE.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/worklogs")]
    [Produces("application/json")]
    [Authorize(Roles = "Provider")]
    public class WorkLogsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IRepository<WorkLog> _repo;

        public WorkLogsController(ApplicationDbContext db, IRepository<WorkLog> repo)
        {
            _db = db;
            _repo = repo;
        }

        [HttpGet("booking/{bookingId:int}")]
        public async Task<ActionResult<WorkLogDto>> GetByBooking([FromRoute] int bookingId)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var wl = await _db.WorkLogs.AsNoTracking()
                .Include(w => w.Booking)
                    .ThenInclude(b => b.ProviderService)
                .FirstOrDefaultAsync(w => w.BookingId == bookingId);

            if (wl is null) return NoContent();

            // ✅ กัน provider คนอื่นมาแอบดู
            if (wl.Booking?.ProviderService?.ProviderId != providerId)
                return Forbid();

            return Ok(new WorkLogDto(
                wl.WorkLogId, wl.BookingId, wl.UserId, wl.CheckInTime, wl.CheckInLat, wl.CheckInLng,
                wl.CheckOutTime, wl.CheckOutLat, wl.CheckOutLng, wl.Note, wl.CreatedAt
            ));
        }

        [HttpPost("checkin")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest req)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var booking = await _db.Bookings
                .Include(b => b.ProviderService)
                .FirstOrDefaultAsync(b => b.BookingId == req.BookingId);

            if (booking is null) return NotFound(new { message = "Booking not found." });

            // ✅ ใช้ providerId จาก token เท่านั้น
            if (booking.ProviderService.ProviderId != providerId)
                return Forbid();

            var allowedFrom = new[] { BookingStatus.Paid, BookingStatus.Assigned, BookingStatus.InProgress };
            if (!allowedFrom.Contains(booking.Status))
                return BadRequest(new { message = $"Booking status not eligible for check-in: {booking.Status}" });

            var wl = await _db.WorkLogs.FirstOrDefaultAsync(w => w.BookingId == req.BookingId);
            if (wl == null)
            {
                wl = new WorkLog
                {
                    BookingId = booking.BookingId,
                    UserId = providerId,
                    CheckInTime = DateTime.UtcNow,
                    CheckInLat = req.Lat,
                    CheckInLng = req.Lng,
                    Note = req.Note,
                    CreatedAt = DateTime.UtcNow
                };
                await _repo.CreateAsync(wl);
            }
            else
            {
                //wl.UserId = providerId;
                //wl.CheckInTime ??= DateTime.UtcNow;
                //wl.CheckInLat = req.Lat;
                //wl.CheckInLng = req.Lng;
                //wl.Note = string.IsNullOrWhiteSpace(req.Note) ? wl.Note : req.Note;
                //await _repo.UpdateAsync(wl);

                wl.CheckInTime ??= DateTime.UtcNow;
                wl.CheckInLat = req.Lat;
                wl.CheckInLng = req.Lng;
                wl.Note = string.IsNullOrWhiteSpace(req.Note) ? wl.Note : req.Note;

            }

            if (BookingStatusRules.CanTransit(booking.Status, BookingStatus.InProgress))
            {
                //booking.Status = BookingStatus.InProgress;
                //booking.UpdatedAt = DateTime.UtcNow;
                //await _db.SaveChangesAsync();

                booking.Status = BookingStatus.InProgress;
                booking.UpdatedAt = DateTime.UtcNow;
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // ✅ ส่ง JSON กลับ ไม่ให้ FE พัง
                return StatusCode(500, new
                {
                    message = "Failed to check-in worklog",
                    detail = ex.InnerException?.Message ?? ex.Message
                });
            }

            return NoContent();
        }

        [Authorize(Roles = "Provider")]
        [HttpPost("checkout")]
        public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest req)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var booking = await _db.Bookings
                .Include(b => b.ProviderService)
                .FirstOrDefaultAsync(b => b.BookingId == req.BookingId);

            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            if (booking.ProviderService.ProviderId != providerId)
                return Forbid();

            var wl = await _db.WorkLogs.FirstOrDefaultAsync(w => w.BookingId == req.BookingId);
            if (wl is null || wl.CheckInTime is null)
                return BadRequest(new { message = "No check-in found for this booking." });

            wl.CheckOutTime ??= DateTime.UtcNow;
            wl.CheckOutLat = req.Lat;
            wl.CheckOutLng = req.Lng;
            wl.Note = string.IsNullOrWhiteSpace(req.Note) ? wl.Note : req.Note;

            await _repo.UpdateAsync(wl);

            if (req.FinalPrice is not null && req.FinalPrice < 0)
                return BadRequest(new { message = "FinalPrice must be >= 0." });

            if (req.FinalPrice is not null)
                booking.FinalPrice = req.FinalPrice;

            if (BookingStatusRules.CanTransit(booking.Status, BookingStatus.Completed))
            {
                booking.Status = BookingStatus.Completed;
                booking.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return NoContent();
        }
    }
}
