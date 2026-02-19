using BookMyService.Models;
using BookMyServiceBE.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/provider")]
    [Produces("application/json")]
    public class ProviderController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ProviderController(ApplicationDbContext db)
        {
            _db = db;
        }

        public sealed record ProviderCalendarItemDto(
            int BookingId,
            string Title,
            DateTime Start,
            DateTime End,
            string Status,
            string CustomerName
        );

        /// <summary>
        /// GET /api/provider/calendar?from=2026-01-01T00:00:00Z&to=2026-01-31T23:59:59Z
        /// </summary>
        [Authorize(Roles = "Provider")]
        [HttpGet("calendar")]
        public async Task<ActionResult<List<ProviderCalendarItemDto>>> GetCalendar(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            if (to <= from)
                return BadRequest(new { message = "`to` must be greater than `from`." });

            // ProviderId จาก JWT
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            // ดึง booking ที่เกี่ยวกับ provider
            // เงื่อนไขช่วงเวลา: ใช้ RequestedStartAt ระหว่าง from-to
            // (ถ้าคุณมี RequestedEndAt ในอนาคต ค่อยเปลี่ยนเป็น overlap logic)
            var items = await _db.Bookings.AsNoTracking()
                .Include(b => b.Customer)
                .Include(b => b.ProviderService)
                .Include(b => b.WorkLog)
                .Where(b => b.ProviderService.ProviderId == providerId)
                .Where(b => b.RequestedStartAt >= from && b.RequestedStartAt <= to)
                .OrderBy(b => b.RequestedStartAt)
                .Select(b => new ProviderCalendarItemDto(
                    b.BookingId,
                    b.JobTitle,
                    b.RequestedStartAt,
                    // ถ้ามีเวลาจริงจาก worklog ให้ใช้จริง / ไม่งั้น start+2ชม
                    b.WorkLog != null && b.WorkLog.CheckOutTime != null
                        ? b.WorkLog.CheckOutTime.Value
                        : b.RequestedStartAt.AddHours(2),
                    b.Status.ToString(),
                    b.Customer != null ? b.Customer.FullName : ""
                ))
                .ToListAsync();

            return Ok(items);
        }
    }
}
