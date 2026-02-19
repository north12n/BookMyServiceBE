using BookMyService.Models;
using BookMyServiceBE.Repository.IRepository;
using BookMyServiceBE.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public sealed class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IJwtTokenService _jwt;
        private readonly PasswordHasher<User> _hasher = new();

        public AuthController(ApplicationDbContext db, IJwtTokenService jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        public sealed record RegisterRequest(
            string Email,
            string Password,
            string FullName,
            UserRole Role,
            string? PhoneNumber
        );

        public sealed record LoginRequest(string Email, string Password);

        public sealed record AuthResponse(
            int UserId,
            string Email,
            string FullName,
            string Role,
            string Token
        );

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
        {
            var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email required." });

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 4)
                return BadRequest(new { message = "Password must be at least 4 characters." });

            if (string.IsNullOrWhiteSpace(req.FullName))
                return BadRequest(new { message = "FullName required." });

            if (req.Role != UserRole.Customer && req.Role != UserRole.Provider)
                return BadRequest(new { message = "Role must be Customer or Provider." });

            var exists = await _db.Users.AsNoTracking()
                .AnyAsync(u => u.Email != null && u.Email.ToLower() == email);

            if (exists)
                return Conflict(new { message = "Email already exists." });

            var user = new User
            {
                Email = email,
                FullName = req.FullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(req.PhoneNumber) ? null : req.PhoneNumber.Trim(),
                UserRole = req.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _hasher.HashPassword(user, req.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var token = _jwt.CreateToken(user);

            return Ok(new AuthResponse(
                user.UserId,
                user.Email ?? "",
                user.FullName,
                user.UserRole.ToString(),
                token
            ));
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
        {
            var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email required." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);
            if (user is null || user.IsActive == false)
                return Unauthorized(new { message = "Email or password incorrect." });

            var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "");
            if (verify == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Email or password incorrect." });

            var token = _jwt.CreateToken(user);

            return Ok(new AuthResponse(
                user.UserId,
                user.Email ?? "",
                user.FullName,
                user.UserRole.ToString(),
                token
            ));
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var me = await _db.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.FullName,
                    u.PhoneNumber,
                    Role = u.UserRole.ToString(),
                    u.IsActive,
                    u.CreatedAt,
                    u.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return me is null ? Unauthorized() : Ok(me);
        }

        [Authorize(Roles = "Provider")]
        [HttpGet("me/provider")]
        public async Task<IActionResult> ProviderMe()
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var me = await _db.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.FullName,
                    u.PhoneNumber,
                    Role = u.UserRole.ToString(),
                    u.BankName,
                    u.BankAccountNumber,
                    u.IsActive,
                    u.CreatedAt,
                    u.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (me is null) return Unauthorized();

            var servicesCount = await _db.ProviderServices.AsNoTracking()
                .CountAsync(s => s.ProviderId == userId);

            var totalEarnings = await _db.Payouts.AsNoTracking()
                .Where(p => p.UserId == userId && p.Status == 2)
                .SumAsync(p => (decimal?)p.NetAmount);

            return Ok(new
            {
                profile = me,
                servicesCount,
                totalEarnings
            });
        }

    }
}
