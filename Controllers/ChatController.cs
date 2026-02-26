using BookMyService.Models;
using BookMyServiceBE.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    [Produces("application/json")]
    public class ChatController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".webm", ".m4v"
        };

        private static readonly HashSet<string> FileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip", ".rar"
        };

        public ChatController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public sealed record ConversationItemDto(
            int CounterpartUserId,
            string CounterpartName,
            string CounterpartRole,
            string? LastMessage,
            DateTime? LastMessageAt,
            int UnreadCount
        );

        public sealed record ContactSearchItemDto(
            int UserId,
            string FullName,
            string UserRole
        );

        public sealed record ChatMessageDto(
            int ChatMessageId,
            int CounterpartUserId,
            int SenderUserId,
            string SenderName,
            string MessageText,
            DateTime CreatedAt,
            bool IsMine,
            bool IsDeleted,
            string? AttachmentPath,
            string? AttachmentName,
            string? AttachmentMimeType,
            long? AttachmentSize,
            string? AttachmentKind
        );

        public sealed record SendMessageRequest(int CounterpartUserId, string MessageText);
        public sealed record EditMessageRequest(string MessageText);
        public sealed class UploadMessageForm
        {
            public int CounterpartUserId { get; set; }
            public string? MessageText { get; set; }
            public IFormFile File { get; set; } = null!;
        }

        [HttpGet("contacts")]
        public async Task<ActionResult<IEnumerable<ContactSearchItemDto>>> SearchContacts([FromQuery] string? keyword)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var text = (keyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return Ok(Array.Empty<ContactSearchItemDto>());

            var query = _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.UserId != userId);

            var normalized = text.ToLower();
            var hasUserIdKeyword = int.TryParse(text, out var parsedUserId);

            var results = await query
                .Where(u =>
                    (u.FullName ?? "").ToLower().Contains(normalized)
                    || (u.Email ?? "").ToLower().Contains(normalized)
                    || (u.PhoneNumber ?? "").ToLower().Contains(normalized)
                    || (hasUserIdKeyword && u.UserId == parsedUserId)
                )
                .OrderBy(u =>
                    (u.FullName ?? "").ToLower().StartsWith(normalized) ? 0 :
                    (u.Email ?? "").ToLower().StartsWith(normalized) ? 1 :
                    (u.PhoneNumber ?? "").ToLower().StartsWith(normalized) ? 2 :
                    (hasUserIdKeyword && u.UserId == parsedUserId) ? 3 : 4
                )
                .ThenBy(u => u.FullName)
                .Take(20)
                .Select(u => new ContactSearchItemDto(
                    u.UserId,
                    u.FullName,
                    u.UserRole.ToString()
                ))
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet("conversations")]
        public async Task<ActionResult<IEnumerable<ConversationItemDto>>> GetConversations()
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var relatedBookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Customer)
                .Include(b => b.ProviderService)
                    .ThenInclude(ps => ps.Provider)
                .Where(b => b.UserId == userId || b.ProviderService.ProviderId == userId)
                .Select(b => new
                {
                    b.BookingId,
                    CounterpartUserId = b.UserId == userId ? b.ProviderService.ProviderId : b.UserId,
                    CounterpartName = b.UserId == userId ? b.ProviderService.Provider.FullName : b.Customer.FullName,
                    CounterpartRole = b.UserId == userId ? UserRole.Provider.ToString() : UserRole.Customer.ToString(),
                })
                .ToListAsync();

            var counterpartItems = relatedBookings
                .GroupBy(x => x.CounterpartUserId)
                .Select(g => (
                    CounterpartUserId: g.Key,
                    CounterpartName: g.First().CounterpartName,
                    CounterpartRole: g.First().CounterpartRole,
                    BookingIds: g.Select(x => x.BookingId).Distinct().ToList()
                ))
                .ToList();

            var directMessages = await _db.ChatMessages
                .AsNoTracking()
                .Where(m => m.RecipientUserId != null && (m.UserId == userId || m.RecipientUserId == userId))
                .ToListAsync();

            var directCounterpartIds = directMessages
                .Select(m => m.UserId == userId ? m.RecipientUserId!.Value : m.UserId)
                .Where(id => id > 0 && id != userId)
                .Distinct()
                .ToList();

            var bookingCounterpartIds = counterpartItems.Select(x => x.CounterpartUserId).ToHashSet();
            var missingCounterpartIds = directCounterpartIds
                .Where(id => !bookingCounterpartIds.Contains(id))
                .ToList();

            if (missingCounterpartIds.Count > 0)
            {
                var missingUsers = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && missingCounterpartIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.FullName, u.UserRole })
                    .ToListAsync();

                counterpartItems.AddRange(missingUsers.Select(u => (
                    CounterpartUserId: u.UserId,
                    CounterpartName: u.FullName,
                    CounterpartRole: u.UserRole.ToString(),
                    BookingIds: new List<int>()
                )));
            }

            var allBookingIds = counterpartItems.SelectMany(x => x.BookingIds).Distinct().ToList();
            var bookingMessages = allBookingIds.Count == 0
                ? new List<ChatMessage>()
                : await _db.ChatMessages
                    .AsNoTracking()
                    .Where(m => m.RelatedBookingId != null && allBookingIds.Contains(m.RelatedBookingId.Value))
                    .ToListAsync();

            var items = counterpartItems.Select(cp =>
            {
                var messagesFromBookings = bookingMessages
                    .Where(m => m.RelatedBookingId != null && cp.BookingIds.Contains(m.RelatedBookingId.Value))
                    .ToList();

                var messagesFromDirect = directMessages
                    .Where(m =>
                        (m.UserId == userId && m.RecipientUserId == cp.CounterpartUserId)
                        || (m.UserId == cp.CounterpartUserId && m.RecipientUserId == userId)
                    )
                    .ToList();

                var messages = messagesFromBookings
                    .Concat(messagesFromDirect)
                    .ToList();

                var lastMessage = messages
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();

                var unreadCount = messages.Count(m => !m.IsDeleted && m.UserId != userId);

                return new ConversationItemDto(
                    cp.CounterpartUserId,
                    cp.CounterpartName,
                    cp.CounterpartRole,
                    ComposeMessagePreview(lastMessage),
                    lastMessage?.CreatedAt,
                    unreadCount
                );
            })
            .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
            .ToList();

            return Ok(items);
        }

        [HttpGet("messages/{counterpartUserId:int}")]
        public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetMessages([FromRoute] int counterpartUserId)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();
            if (counterpartUserId <= 0 || counterpartUserId == userId)
                return BadRequest(new { message = "CounterpartUserId is invalid." });

            var sharedBookingIds = await GetSharedBookingIdsAsync(userId, counterpartUserId);

            var messages = await _db.ChatMessages
                .AsNoTracking()
                .Include(m => m.Sender)
                .Where(m =>
                    (m.RelatedBookingId != null && sharedBookingIds.Contains(m.RelatedBookingId.Value))
                    || (m.RecipientUserId != null && (
                        (m.UserId == userId && m.RecipientUserId == counterpartUserId)
                        || (m.UserId == counterpartUserId && m.RecipientUserId == userId)
                    ))
                )
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto(
                    m.ChatMessageId,
                    counterpartUserId,
                    m.UserId,
                    m.Sender.FullName,
                    m.IsDeleted ? "ข้อความถูกลบแล้ว" : m.MessageText,
                    m.CreatedAt,
                    m.UserId == userId,
                    m.IsDeleted,
                    m.IsDeleted ? null : m.AttachmentPath,
                    m.IsDeleted ? null : m.AttachmentName,
                    m.IsDeleted ? null : m.AttachmentMimeType,
                    m.IsDeleted ? null : m.AttachmentSize,
                    m.IsDeleted ? null : GetAttachmentKind(m.AttachmentMimeType, m.AttachmentPath)
                ))
                .ToListAsync();

            return Ok(messages);
        }

        [HttpPost("messages")]
        public async Task<ActionResult<ChatMessageDto>> SendMessage([FromBody] SendMessageRequest req)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            if (req.CounterpartUserId <= 0 || req.CounterpartUserId == userId)
                return BadRequest(new { message = "CounterpartUserId is invalid." });

            if (string.IsNullOrWhiteSpace(req.MessageText))
                return BadRequest(new { message = "MessageText is required." });

            var counterpartExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserId == req.CounterpartUserId && u.IsActive);
            if (!counterpartExists)
                return NotFound(new { message = "ไม่พบผู้ใช้งานปลายทาง" });

            var sharedBookingIds = await GetSharedBookingIdsAsync(userId, req.CounterpartUserId);

            var sender = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (sender is null) return Unauthorized();

            var latestBookingId = sharedBookingIds.Count > 0 ? sharedBookingIds.Max() : (int?)null;

            var message = new ChatMessage
            {
                UserId = userId,
                RecipientUserId = req.CounterpartUserId,
                RelatedBookingId = latestBookingId,
                SenderRole = SenderRole.User,
                MessageText = req.MessageText.Trim(),
                CreatedAt = DateTime.UtcNow,
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();

            return Ok(new ChatMessageDto(
                message.ChatMessageId,
                req.CounterpartUserId,
                message.UserId,
                sender.FullName,
                message.MessageText,
                message.CreatedAt,
                true,
                false,
                null,
                null,
                null,
                null,
                null
            ));
        }

        [HttpPost("messages/upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ChatMessageDto>> SendAttachment([FromForm] UploadMessageForm form)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            if (form.CounterpartUserId <= 0 || form.CounterpartUserId == userId)
                return BadRequest(new { message = "CounterpartUserId is invalid." });

            if (form.File is null || form.File.Length == 0)
                return BadRequest(new { message = "File is required." });

            var counterpartExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserId == form.CounterpartUserId && u.IsActive);
            if (!counterpartExists)
                return NotFound(new { message = "ไม่พบผู้ใช้งานปลายทาง" });

            var sharedBookingIds = await GetSharedBookingIdsAsync(userId, form.CounterpartUserId);

            var sender = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (sender is null) return Unauthorized();

            var fileValidation = ValidateAttachment(form.File);
            if (!fileValidation.IsValid)
                return BadRequest(new { message = fileValidation.ErrorMessage });

            var conversationKey = GetConversationKey(userId, form.CounterpartUserId);
            var (publicPath, mimeType) = await SaveAttachmentAsync(form.File, conversationKey);

            var latestBookingId = sharedBookingIds.Count > 0 ? sharedBookingIds.Max() : (int?)null;

            var message = new ChatMessage
            {
                UserId = userId,
                RecipientUserId = form.CounterpartUserId,
                RelatedBookingId = latestBookingId,
                SenderRole = SenderRole.User,
                MessageText = string.IsNullOrWhiteSpace(form.MessageText) ? "ส่งไฟล์แนบ" : form.MessageText.Trim(),
                AttachmentPath = publicPath,
                AttachmentName = Path.GetFileName(form.File.FileName),
                AttachmentMimeType = mimeType,
                AttachmentSize = form.File.Length,
                CreatedAt = DateTime.UtcNow,
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();

            return Ok(new ChatMessageDto(
                message.ChatMessageId,
                form.CounterpartUserId,
                message.UserId,
                sender.FullName,
                message.MessageText,
                message.CreatedAt,
                true,
                false,
                message.AttachmentPath,
                message.AttachmentName,
                message.AttachmentMimeType,
                message.AttachmentSize,
                GetAttachmentKind(message.AttachmentMimeType, message.AttachmentPath)
            ));
        }

        [HttpPut("messages/{chatMessageId:int}")]
        public async Task<IActionResult> EditMessage([FromRoute] int chatMessageId, [FromBody] EditMessageRequest req)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.MessageText))
                return BadRequest(new { message = "MessageText is required." });

            var message = await _db.ChatMessages.FirstOrDefaultAsync(m => m.ChatMessageId == chatMessageId);
            if (message is null) return NotFound();

            if (message.UserId != userId) return Forbid();
            if (message.IsDeleted) return Conflict(new { message = "Cannot edit deleted message." });

            if (message.RelatedBookingId is not null)
            {
                var allowed = await IsBookingAccessibleAsync(message.RelatedBookingId.Value, userId);
                if (!allowed) return Forbid();
            }

            message.MessageText = req.MessageText.Trim();
            message.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("messages/{chatMessageId:int}")]
        public async Task<IActionResult> DeleteMessage([FromRoute] int chatMessageId)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var message = await _db.ChatMessages.FirstOrDefaultAsync(m => m.ChatMessageId == chatMessageId);
            if (message is null) return NotFound();

            if (message.UserId != userId) return Forbid();

            if (message.RelatedBookingId is not null)
            {
                var allowed = await IsBookingAccessibleAsync(message.RelatedBookingId.Value, userId);
                if (!allowed) return Forbid();
            }

            if (message.IsDeleted) return NoContent();

            if (!string.IsNullOrWhiteSpace(message.AttachmentPath))
                DeletePhysicalFile(message.AttachmentPath);

            message.IsDeleted = true;
            message.DeletedAt = DateTime.UtcNow;
            message.UpdatedAt = DateTime.UtcNow;
            message.MessageText = "";
            message.AttachmentPath = null;
            message.AttachmentName = null;
            message.AttachmentMimeType = null;
            message.AttachmentSize = null;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<List<int>> GetSharedBookingIdsAsync(int userId, int counterpartUserId)
        {
            return await _db.Bookings
                .AsNoTracking()
                .Include(b => b.ProviderService)
                .Where(b =>
                    (b.UserId == userId && b.ProviderService.ProviderId == counterpartUserId)
                    ||
                    (b.UserId == counterpartUserId && b.ProviderService.ProviderId == userId)
                )
                .Select(b => b.BookingId)
                .Distinct()
                .ToListAsync();
        }

        private async Task<bool> IsBookingAccessibleAsync(int bookingId, int userId)
        {
            return await _db.Bookings
                .AsNoTracking()
                .Include(b => b.ProviderService)
                .AnyAsync(b => b.BookingId == bookingId
                    && (b.UserId == userId || b.ProviderService.ProviderId == userId));
        }

        private static string ComposeMessagePreview(ChatMessage? message)
        {
            if (message is null) return "";
            if (!string.IsNullOrWhiteSpace(message.AttachmentPath)) return "ส่งไฟล์แนบ";
            return message.MessageText;
        }

        private static (bool IsValid, string? ErrorMessage) ValidateAttachment(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var kind = GetAttachmentKind(file.ContentType, ext);

            if (kind is null)
                return (false, "ประเภทไฟล์ไม่รองรับ");

            var maxSize = kind switch
            {
                "image" => 10 * 1024 * 1024,
                "video" => 50 * 1024 * 1024,
                _ => 20 * 1024 * 1024,
            };

            if (file.Length > maxSize)
                return (false, $"ไฟล์ใหญ่เกินกำหนด (สูงสุด {maxSize / 1024 / 1024}MB)");

            return (true, null);
        }

        private async Task<(string PublicPath, string MimeType)> SaveAttachmentAsync(IFormFile file, string conversationKey)
        {
            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
                Directory.CreateDirectory(webRoot);
                _env.WebRootPath = webRoot;
            }

            var folder = Path.Combine(webRoot, "uploads", "chat", conversationKey);
            Directory.CreateDirectory(folder);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, fileName);

            await using var fs = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(fs);

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(file.FileName, out var contentType))
                contentType = "application/octet-stream";

            return ($"/uploads/chat/{conversationKey}/{fileName}", contentType);
        }

        private static string GetConversationKey(int userId1, int userId2)
        {
            var min = Math.Min(userId1, userId2);
            var max = Math.Max(userId1, userId2);
            return $"u-{min}-{max}";
        }

        private void DeletePhysicalFile(string publicPath)
        {
            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot)) return;

            var rel = publicPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
            var fullPath = Path.Combine(webRoot, rel);
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }

        private static string? GetAttachmentKind(string? mimeType, string? filePathOrExt)
        {
            var ext = filePathOrExt is null
                ? ""
                : (filePathOrExt.StartsWith('.') ? filePathOrExt : Path.GetExtension(filePathOrExt)).ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(mimeType) && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return "image";
            if (!string.IsNullOrWhiteSpace(mimeType) && mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return "video";

            if (ImageExtensions.Contains(ext)) return "image";
            if (VideoExtensions.Contains(ext)) return "video";
            if (FileExtensions.Contains(ext)) return "file";

            return null;
        }
    }
}
