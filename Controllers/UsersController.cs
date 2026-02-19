using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookMyService.Models;
using System.Security.Claims;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public UsersController(ApplicationDbContext db)
        {
            _db = db;
        }

        // DTOs for request/response
        public class AdminUserInfoDto
        {
            public int UserId { get; set; }
            public string FullName { get; set; } = null!;
            public string Email { get; set; } = null!;
            public string PhoneNumber { get; set; } = null!;
            public int UserRole { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }

            // Provider-specific fields
            public string? BankName { get; set; }
            public string? BankAccountNumber { get; set; }
            public int? ServicesCount { get; set; }
            public int? CompletedJobsCount { get; set; }
            public decimal? AverageRating { get; set; }
            public decimal? TotalEarnings { get; set; }
        }

        public class UpdateUserStatusDto
        {
            public bool IsActive { get; set; }
        }

        public class ApproveProviderDto
        {
            public bool Approved { get; set; }
            public string? Reason { get; set; }
        }

        /// <summary>
        /// GET /api/admin/users - Get all users with stats
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<AdminUserInfoDto>>> GetAllUsers()
        {
            try
            {
                var users = await _db.Users
                    .AsNoTracking()
                    .OrderByDescending(u => u.CreatedAt)
                    .ToListAsync();

                var userDtos = new List<AdminUserInfoDto>();

                foreach (var user in users)
                {
                    var dto = new AdminUserInfoDto
                    {
                        UserId = user.UserId,
                        FullName = user.FullName,
                        Email = user.Email ?? "",
                        PhoneNumber = user.PhoneNumber ?? "",
                        UserRole = (int)user.UserRole,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt,
                        BankName = user.BankName,
                        BankAccountNumber = user.BankAccountNumber,
                    };

                    // If provider, calculate stats
                    if (user.UserRole == UserRole.Provider)
                    {
                        // Count services
                        dto.ServicesCount = await _db.ProviderServices
                            .Where(ps => ps.ProviderId == user.UserId && ps.IsActive)
                            .CountAsync();

                        // Count completed jobs
                        dto.CompletedJobsCount = await _db.Bookings
                            .Where(b => b.ProviderService.ProviderId == user.UserId &&
                                        b.Status == BookingStatus.Completed)
                            .CountAsync();

                        // Calculate average rating
                        var ratings = await _db.Reviews
                            .Where(r => r.Booking.ProviderService.ProviderId == user.UserId)
                            .Select(r => r.Rating)
                            .ToListAsync();

                        if (ratings.Count > 0)
                        {
                            dto.AverageRating = (decimal)ratings.Average();
                        }

                        // Calculate total earnings
                        var completedBookings = await _db.Bookings
                            .Where(b => b.ProviderService.ProviderId == user.UserId &&
                                        b.Status == BookingStatus.Completed)
                            .Select(b => b.FinalPrice ?? b.EstimatedPrice)
                            .ToListAsync();

                        if (completedBookings.Count > 0)
                        {
                            dto.TotalEarnings = completedBookings.Sum();
                        }
                    }

                    userDtos.Add(dto);
                }

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการดึงข้อมูลผู้ใช้", error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/admin/users/{id} - Get user by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<AdminUserInfoDto>> GetUserById(int id)
        {
            try
            {
                var user = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    return NotFound(new { message = "ไม่พบผู้ใช้งาน" });
                }

                var dto = new AdminUserInfoDto
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email ?? "",
                    PhoneNumber = user.PhoneNumber ?? "",
                    UserRole = (int)user.UserRole,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    BankName = user.BankName,
                    BankAccountNumber = user.BankAccountNumber,
                };

                if (user.UserRole == UserRole.Provider)
                {
                    dto.ServicesCount = await _db.ProviderServices
                        .Where(ps => ps.ProviderId == user.UserId && ps.IsActive)
                        .CountAsync();

                    dto.CompletedJobsCount = await _db.Bookings
                        .Where(b => b.ProviderService.ProviderId == user.UserId &&
                                    b.Status == BookingStatus.Completed)
                        .CountAsync();

                    var ratings = await _db.Reviews
                        .Where(r => r.Booking.ProviderService.ProviderId == user.UserId)
                        .Select(r => r.Rating)
                        .ToListAsync();

                    if (ratings.Count > 0)
                    {
                        dto.AverageRating = (decimal)ratings.Average();
                    }

                    var completedBookings = await _db.Bookings
                        .Where(b => b.ProviderService.ProviderId == user.UserId &&
                                    b.Status == BookingStatus.Completed)
                        .Select(b => b.FinalPrice ?? b.EstimatedPrice)
                        .ToListAsync();

                    if (completedBookings.Count > 0)
                    {
                        dto.TotalEarnings = completedBookings.Sum();
                    }
                }

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการดึงข้อมูลผู้ใช้", error = ex.Message });
            }
        }

        /// <summary>
        /// PATCH /api/admin/users/{id}/status - Update user active status
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusDto request)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    return NotFound(new { message = "ไม่พบผู้ใช้งาน" });
                }

                user.IsActive = request.IsActive;
                user.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = $"อัปเดตสถานะผู้ใช้งานเป็น {(request.IsActive ? "ใช้งาน" : "ปิดใช้งาน")} สำเร็จ",
                    userId = id,
                    isActive = request.IsActive
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการอัปเดตสถานะ", error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/users/{id}/approve-provider - Approve provider registration
        /// </summary>
        [HttpPost("{id}/approve-provider")]
        public async Task<IActionResult> ApproveProvider(int id, [FromBody] ApproveProviderDto request)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    return NotFound(new { message = "ไม่พบผู้ใช้งาน" });
                }

                if (user.UserRole != UserRole.Provider)
                {
                    return BadRequest(new { message = "ผู้ใช้งานนี้ไม่ใช่ผู้ให้บริการ" });
                }

                if (request.Approved)
                {
                    user.IsActive = true;
                    user.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    return Ok(new
                    {
                        message = "อนุมัติผู้ให้บริการสำเร็จ",
                        userId = id,
                        isApproved = true
                    });
                }
                else
                {
                    // Reject provider
                    user.IsActive = false;
                    user.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    return Ok(new
                    {
                        message = "ปฏิเสธผู้ให้บริการสำเร็จ",
                        userId = id,
                        isApproved = false,
                        reason = request.Reason
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการอนุมัติผู้ให้บริการ", error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE /api/admin/users/{id} - Delete user (soft delete via IsActive)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    return NotFound(new { message = "ไม่พบผู้ใช้งาน" });
                }

                // Soft delete - just set IsActive to false
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "ลบผู้ใช้งานสำเร็จ",
                    userId = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการลบผู้ใช้งาน", error = ex.Message });
            }
        }
    }
}
