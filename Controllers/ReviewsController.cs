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
    [Route("api/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IRepository<Review> _repo;

        public ReviewsController(ApplicationDbContext db, IRepository<Review> repo)
        {
            _db = db;
            _repo = repo;
        }

        // =========================
        // Customer: สร้างรีวิว (รีวิวได้ครั้งเดียว)
        // =========================
        [Authorize(Roles = "Customer")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            // validation เพิ่มความชัดเจน
            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { message = "Rating must be between 1 and 5." });

            var booking = await _db.Bookings
                .Include(b => b.Review)
                .FirstOrDefaultAsync(b => b.BookingId == dto.BookingId);

            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            if (booking.UserId != userId)
                return Forbid();

            if (booking.Status != BookingStatus.Completed)
                return BadRequest(new { message = "Booking must be completed before review." });

            if (booking.Review != null)
                return Conflict(new { message = "Review already exists for this booking." });

            var review = new Review
            {
                BookingId = booking.BookingId,
                UserId = userId,
                Rating = dto.Rating,
                Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _repo.CreateAsync(review);

            // จะใช้ Ok ก็ได้ แต่ 201 จะมาตรฐานกว่า
            return CreatedAtAction(nameof(MyReviews), null, new
            {
                message = "Review submitted successfully.",
                reviewId = review.ReviewId,
                bookingId = review.BookingId
            });
        }

        // =========================
        // Public: รีวิวของ provider (เอาไปแสดงหน้า provider detail)
        // =========================
        [HttpGet("provider/{providerId:int}")]
        public async Task<IActionResult> GetByProvider([FromRoute] int providerId)
        {
            var reviews = await _db.Reviews.AsNoTracking()
                .Include(r => r.Booking)
                    .ThenInclude(b => b.ProviderService)
                .Include(r => r.Reviewer)
                .Where(r => r.Booking.ProviderService.ProviderId == providerId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewResponseDto(
                    r.ReviewId,
                    r.BookingId,
                    r.Rating,
                    r.Comment,
                    r.Reviewer.FullName,
                    r.CreatedAt
                ))
                .ToListAsync();

            return Ok(reviews);
        }

        // =========================
        // Provider: summary (avg)
        // =========================
        [Authorize(Roles = "Provider")]
        [HttpGet("provider/me/summary")]
        public async Task<IActionResult> MyReviewSummary()
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var query = _db.Reviews.AsNoTracking()
                .Where(r => r.Booking.ProviderService.ProviderId == providerId);

            var count = await query.CountAsync();
            var avg = count == 0 ? 0 : await query.AverageAsync(r => r.Rating);

            return Ok(new
            {
                totalReviews = count,
                averageRating = Math.Round(avg, 2)
            });
        }

        // =========================
        // Customer: ดูรีวิวของฉันทั้งหมด (ที่เคยเขียนแล้ว)
        // =========================
        [Authorize(Roles = "Customer")]
        [HttpGet("customer/me")]
        public async Task<IActionResult> MyReviews()
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var items = await _db.Reviews.AsNoTracking()
                .Include(r => r.Booking)
                    .ThenInclude(b => b.ProviderService)
                        .ThenInclude(ps => ps.Provider)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new MyReviewItemDto(
                    r.ReviewId,
                    r.BookingId,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt,
                    r.Booking.ProviderService.Title,
                    r.Booking.ProviderService.ProviderId,
                    r.Booking.ProviderService.Provider.FullName
                ))
                .ToListAsync();

            return Ok(items);
        }

        // =========================
        // Customer: แก้ไขรีวิวของ booking นี้
        // =========================
        [Authorize(Roles = "Customer")]
        [HttpPut("{bookingId:int}")]
        public async Task<IActionResult> Update([FromRoute] int bookingId, [FromBody] UpdateReviewDto dto)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { message = "Rating must be between 1 and 5." });

            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.BookingId == bookingId);

            if (review is null)
                return NotFound(new { message = "Review not found for this booking." });

            if (review.UserId != userId)
                return Forbid();

            // (optional) strict: ต้องเป็นงาน Completed เท่านั้นถึงแก้ได้
            var booking = await _db.Bookings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            if (booking.UserId != userId)
                return Forbid();

            if (booking.Status != BookingStatus.Completed)
                return BadRequest(new { message = "Booking must be completed before updating review." });

            review.Rating = dto.Rating;
            review.Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();

            await _repo.UpdateAsync(review);
            return NoContent();
        }

        // =========================
        // Customer: ลบรีวิวของ booking นี้
        // =========================
        [Authorize(Roles = "Customer")]
        [HttpDelete("{bookingId:int}")]
        public async Task<IActionResult> Delete([FromRoute] int bookingId)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.BookingId == bookingId);

            if (review is null)
                return NotFound(new { message = "Review not found for this booking." });

            if (review.UserId != userId)
                return Forbid();

            await _repo.RemoveAsync(review);
            return NoContent();
        }
    }
}
