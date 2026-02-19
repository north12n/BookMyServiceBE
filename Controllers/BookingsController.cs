using BookMyService.Models;
using BookMyServiceBE.Models;
using BookMyServiceBE.Models.Dto;
using BookMyServiceBE.Repository;
using BookMyServiceBE.Repository.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/bookings")]
    public class BookingsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IRepository<Booking> _repo;
        private readonly IReceiptPdfService _receiptPdf;

        public BookingsController(ApplicationDbContext db, IRepository<Booking> repo, IReceiptPdfService receiptPdf)
        {
            _db = db;
            _repo = repo;
            _receiptPdf = receiptPdf;
        }

        // อ่านทั้งหมด (เพื่อ debug/หลังบ้าน)
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<BookingListItemDto>>> GetAll()
        {
            var items = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.ProviderService).ThenInclude(s => s.Provider)
                .Include(b => b.ProviderService).ThenInclude(s => s.ServiceCategory)
                .OrderByDescending(x => x.CreatedAt)
                .Select(BookingListItemDto.Projection)
                .ToListAsync();

            return Ok(items);
        }

        // list + filter + paginate (query ทั้งหมด optional)
        [HttpGet]
        public async Task<ActionResult<PagedResult<BookingListItemDto>>> List([FromQuery] BookingQuery q)
        {
            var query = _db.Bookings.AsNoTracking()
                .Include(b => b.ProviderService).ThenInclude(s => s.Provider)
                .Include(b => b.ProviderService).ThenInclude(s => s.ServiceCategory)
                .AsQueryable();

            if (q.CustomerId is not null) query = query.Where(x => x.UserId == q.CustomerId);
            if (q.ProviderId is not null) query = query.Where(x => x.ProviderService.ProviderId == q.ProviderId);
            if (q.Status is not null) query = query.Where(x => x.Status == q.Status);
            if (q.From is not null) query = query.Where(x => x.CreatedAt >= q.From);
            if (q.To is not null) query = query.Where(x => x.CreatedAt <= q.To);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(BookingListItemDto.Projection)
                .ToListAsync();

            return Ok(new PagedResult<BookingListItemDto>(items, total, q.Page, q.PageSize));
        }

        // รายละเอียด
        [HttpGet("{id:int}")]
        public async Task<ActionResult<BookingDetailDto>> GetById([FromRoute] int id)
        {
            var data = await _db.Bookings.AsNoTracking()
                .Include(b => b.ProviderService).ThenInclude(s => s.Provider)
                .Include(b => b.ProviderService).ThenInclude(s => s.ServiceCategory)
                .Where(b => b.BookingId == id)
                .Select(BookingDetailDto.Projection)
                .FirstOrDefaultAsync();

            return data is null ? NotFound() : Ok(data);
        }

        // ใส่ไว้ใน BookingsController (ระดับ class)

        // สถานะที่ถือว่า "กันเวลางาน" (ยังไม่ยกเลิก และงานยังไม่จบ)
        private static readonly BookingStatus[] BlockingStatuses = new[]
        {
            BookingStatus.PendingPayment,
            BookingStatus.Paid,
            BookingStatus.Assigned,
            BookingStatus.InProgress
        };

        // เช็คว่า provider ว่างไหม ในช่วงเวลา start-end (UTC)
        private async Task<bool> IsProviderBusy(int providerId, DateTime start, DateTime end)
        {
            var blocked = await _db.ProviderAvailabilityBlocks
                .AsNoTracking()
                .AnyAsync(b =>
                    b.ProviderId == providerId &&
                    b.EndUtc > start &&
                    b.StartUtc < end
                );

            if (blocked) return true;

            var conflict = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.ProviderService)
                .Where(b => b.ProviderService.ProviderId == providerId)
                .Where(b => BlockingStatuses.Contains(b.Status))
                .AnyAsync(b =>
                    b.RequestedStartAt.AddHours(2) > start &&
                    b.RequestedStartAt < end
                );

            return conflict;
        }

        // จองแบบสั้น: ใช้ข้อมูลจากโปรไฟล์ลูกค้า + BasePrice
        [HttpPost("quick")]
        public async Task<ActionResult<CreatedBookingDto>> QuickCreate([FromBody] QuickCreateBookingDto dto)
        {
            var customer = await _db.Users.FirstOrDefaultAsync(u => u.UserId == dto.UserId);
            if (customer is null) return BadRequest(new { message = "UserId not found." });
            if (customer.UserRole != UserRole.Customer) return BadRequest(new { message = "User is not a customer." });

            var service = await _db.ProviderServices
                .Include(s => s.Provider)
                .Include(s => s.ServiceCategory)
                .FirstOrDefaultAsync(s => s.ProviderServiceId == dto.ProviderServiceId && s.IsActive);

            if (service is null) return BadRequest(new { message = "ProviderServiceId not found or inactive." });

            if (dto.RequestedStartAt < DateTime.UtcNow.AddMinutes(-1))
                return BadRequest(new { message = "RequestedStartAt must be in the future." });

            if (string.IsNullOrWhiteSpace(customer.AddressLine) ||
                string.IsNullOrWhiteSpace(customer.District) ||
                string.IsNullOrWhiteSpace(customer.Province) ||
                string.IsNullOrWhiteSpace(customer.PostalCode))
            {
                return BadRequest(new { message = "Customer profile has no address. Please update profile or use full create." });
            }

            // ✅ เช็ค block + overlap (หลังจากรู้ providerId แล้ว)
            var start = dto.RequestedStartAt;
            var end = dto.RequestedStartAt.AddHours(2); // TODO: ถ้ามี end จริง ให้เปลี่ยน
            var providerId = service.ProviderId;

            if (await IsProviderBusy(providerId, start, end))
            {
                return Conflict(new
                {
                    message = "Provider is not available in selected time. Please choose another day/time."
                });
            }

            var booking = new Booking
            {
                BookingCode = await GenerateUniqueBookingCodeAsync(),
                UserId = dto.UserId,
                ProviderServiceId = dto.ProviderServiceId,
                JobTitle = service.Title,
                JobDescription = null,
                RequestedStartAt = dto.RequestedStartAt,
                AddressLine = customer.AddressLine!,
                District = customer.District!,
                Province = customer.Province!,
                PostalCode = customer.PostalCode!,
                EstimatedPrice = service.BasePrice,
                FinalPrice = null,
                Status = BookingStatus.PendingPayment,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.CreateAsync(booking);

            return CreatedAtAction(nameof(GetById), new { id = booking.BookingId },
                new CreatedBookingDto(booking.BookingId, booking.BookingCode, booking.Status));
        }


        // จองแบบเต็ม
        [HttpPost]
        public async Task<ActionResult<CreatedBookingDto>> Create([FromBody] CreateBookingDto dto)
        {
            var customer = await _db.Users.FirstOrDefaultAsync(u => u.UserId == dto.UserId);
            if (customer is null) return BadRequest(new { message = "UserId not found." });
            if (customer.UserRole != UserRole.Customer) return BadRequest(new { message = "User is not a customer." });

            var service = await _db.ProviderServices
                .Include(s => s.Provider)
                .FirstOrDefaultAsync(s => s.ProviderServiceId == dto.ProviderServiceId && s.IsActive);

            if (service is null) return BadRequest(new { message = "ProviderServiceId not found or inactive." });

            if (dto.EstimatedPrice < 0) return BadRequest(new { message = "EstimatedPrice must be >= 0." });

            if (dto.RequestedStartAt < DateTime.UtcNow.AddMinutes(-1))
                return BadRequest(new { message = "RequestedStartAt must be in the future." });

            // ✅ เช็ค block + overlap
            var start = dto.RequestedStartAt;
            var end = dto.RequestedStartAt.AddHours(2); // TODO: ถ้ามี end จริง ให้เปลี่ยน
            var providerId = service.ProviderId;

            if (await IsProviderBusy(providerId, start, end))
            {
                return Conflict(new
                {
                    message = "Provider is not available in selected time. Please choose another day/time."
                });
            }

            var booking = new Booking
            {
                BookingCode = await GenerateUniqueBookingCodeAsync(),
                UserId = dto.UserId,
                ProviderServiceId = dto.ProviderServiceId,
                JobTitle = dto.JobTitle.Trim(),
                JobDescription = dto.JobDescription?.Trim(),
                RequestedStartAt = dto.RequestedStartAt,
                AddressLine = dto.AddressLine.Trim(),
                District = dto.District.Trim(),
                Province = dto.Province.Trim(),
                PostalCode = dto.PostalCode.Trim(),
                EstimatedPrice = dto.EstimatedPrice,
                FinalPrice = null,
                Status = BookingStatus.PendingPayment,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.CreateAsync(booking);

            return CreatedAtAction(nameof(GetById), new { id = booking.BookingId },
                new CreatedBookingDto(booking.BookingId, booking.BookingCode, booking.Status));
        }


        // เปลี่ยนสถานะตามกติกา (state machine)
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> ChangeStatus([FromRoute] int id, [FromBody] ChangeBookingStatusDto dto)
        {
            var entity = await _db.Bookings.FirstOrDefaultAsync(b => b.BookingId == id);
            if (entity is null) return NotFound();

            if (!BookingStatusRules.CanTransit(entity.Status, dto.NewStatus))
                return BadRequest(new { message = $"Invalid transition: {entity.Status} -> {dto.NewStatus}" });

            if (dto.NewStatus == BookingStatus.Completed && dto.FinalPrice is not null)
            {
                if (dto.FinalPrice < 0) return BadRequest(new { message = "FinalPrice must be >= 0." });
                entity.FinalPrice = dto.FinalPrice;
            }

            entity.Status = dto.NewStatus;
            entity.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(entity);
            return NoContent();
        }

        // ============ Utilities ============
        // โค้ดอ่านง่าย เช่น BK-20251229-153045-482
        private static string GenerateBookingCode()
            => $"BK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

        // กันชนซ้ำด้วยการตรวจ DB + retry เล็กน้อย
        private async Task<string> GenerateUniqueBookingCodeAsync()
        {
            for (var i = 0; i < 3; i++)
            {
                var code = GenerateBookingCode();
                var exists = await _db.Bookings.AsNoTracking().AnyAsync(b => b.BookingCode == code);
                if (!exists) return code;
                await Task.Delay(10);
            }
            return GenerateBookingCode();
        }

        public record MockPayResponse(int BookingId, string BookingCode, BookingStatus Status, DateTime PaidAt);

        /// <summary>
        /// Mock ชำระเงิน: อนุญาตเฉพาะสถานะ PendingPayment -> Paid
        /// </summary>
        [HttpPost("{id:int}/pay/mock")]
        public async Task<ActionResult<MockPayResponse>> MockPay([FromRoute] int id)
        {
            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.BookingId == id);
            if (booking is null) return NotFound();

            // จ่ายซ้ำ (idempotent): ถ้าเคย Paid แล้ว ก็คืนค่าเดิมได้เลย
            if (booking.Status == BookingStatus.Paid)
            {
                var paidAt = booking.UpdatedAt ?? booking.CreatedAt; // ตอนนี้เป็น DateTime แล้ว
                                                                     // ไม่ต้อง ?? ซ้ำอีก
                return Ok(new MockPayResponse(booking.BookingId, booking.BookingCode, booking.Status, paidAt));


            }

            // ต้องอยู่ใน PendingPayment เท่านั้น
            if (booking.Status != BookingStatus.PendingPayment)
                return BadRequest(new { message = $"Booking is {booking.Status}, cannot pay." });

            // ถ้ามีกติกา state machine ให้ตรวจด้วย (ถ้าไม่มี BookingStatusRules ก็ข้าม logic นี้ได้)
            if (BookingStatusRules.CanTransit(booking.Status, BookingStatus.Paid) == false)
                return BadRequest(new { message = $"Invalid transition: {booking.Status} -> {BookingStatus.Paid}" });

            booking.Status = BookingStatus.Paid;
            booking.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(booking);

            // (ถ้าคุณมีตาราง Payment และอยากบันทึก mock payment record ไว้จริง ให้เพิ่มในจุดนี้)
            // _db.Payments.Add(new Payment { ... }); await _db.SaveChangesAsync();

            return Ok(new MockPayResponse(booking.BookingId, booking.BookingCode, booking.Status, booking.UpdatedAt.Value));
        }

        /// <summary>ดาวน์โหลดใบเสร็จ (PDF) ของการจอง</summary>
        [HttpGet("{id:int}/receipt")]
        [Produces("application/pdf")]
        public async Task<IActionResult> GetReceipt([FromRoute] int id)
        {
            var booking = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Customer)  // เอาชื่อลูกค้า
                .Include(b => b.ProviderService).ThenInclude(s => s.Provider)
                .Include(b => b.ProviderService).ThenInclude(s => s.ServiceCategory)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null)
                return NotFound();

            var pdfBytes = _receiptPdf.CreateBookingReceiptPdf(booking);
            var fileName = $"{booking.BookingCode}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpGet("by-code/{code}")]
        public async Task<ActionResult<BookingDetailDto>> GetByCode([FromRoute] string code)
        {
            var data = await _db.Bookings.AsNoTracking()
                .Include(b => b.ProviderService).ThenInclude(s => s.Provider)
                .Include(b => b.ProviderService).ThenInclude(s => s.ServiceCategory)
                .Where(b => b.BookingCode == code)
                .Select(BookingDetailDto.Projection)
                .FirstOrDefaultAsync();

            return data is null ? NotFound() : Ok(data);
        }

        /// <summary>แอดมิน: โอนเงินให้ผู้ให้บริการ (mock)</summary>
        [HttpPost("{id:int}/payout/mock")]
        public async Task<IActionResult> PayoutProviderMock([FromRoute] int id, [FromQuery] decimal? feePercent = 10m)
        {
            var booking = await _db.Bookings
                .Include(b => b.ProviderService).ThenInclude(s => s.Provider)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound(new { message = "Booking not found." });

            // ต้องเสร็จงานก่อน
            if (booking.Status != BookingStatus.Completed)
                return BadRequest(new { message = "Booking must be Completed before payout." });

            // กันจ่ายซ้ำ
            var exists = await _db.Payouts.AsNoTracking().AnyAsync(p => p.BookingId == id);
            if (exists) return Conflict(new { message = "Payout already created for this booking." });

            // จำนวนเงินจาก FinalPrice หรือ EstimatedPrice
            var amount = booking.FinalPrice ?? booking.EstimatedPrice;
            if (amount <= 0) return BadRequest(new { message = "Invalid amount." });

            // คำนวณค่าธรรมเนียม/คอมมิชชั่น
            var feeRate = (feePercent ?? 10m) / 100m;
            var fee = Math.Round(amount * feeRate, 2, MidpointRounding.AwayFromZero);
            var net = amount - fee;

            // หา providerId จากบริการ
            var providerId = booking.ProviderService?.Provider?.UserId ?? 0;
            if (providerId <= 0) return BadRequest(new { message = "Provider not found on this booking." });

            var payout = new Payout
            {
                BookingId = booking.BookingId,
                UserId = providerId,
                GrossAmount = amount,
                FeeAmount = fee,
                NetAmount = net,
                Status = 2,                 // 2 = Paid (จ่ายแล้ว)
                SettledAt = DateTime.UtcNow // เวลาที่โอน
                                            // CreatedAt = default = UtcNow (จาก model)
            };

            _db.Payouts.Add(payout);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                payout.PayoutId,
                payout.BookingId,
                payout.UserId,
                payout.GrossAmount,
                payout.FeeAmount,
                payout.NetAmount,
                payout.Status,
                payout.SettledAt
            });
        }

        /// <summary>รอโอน: Booking Completed แต่ยังไม่มี Payout</summary>
        [HttpGet("payouts/pending")]
        public async Task<IActionResult> GetPendingPayouts()
        {
            var pending = await (from b in _db.Bookings.AsNoTracking()
                                 join p in _db.Payouts.AsNoTracking()
                                     on b.BookingId equals p.BookingId into payoutGroup
                                 from p in payoutGroup.DefaultIfEmpty()
                                 where b.Status == BookingStatus.Completed
                                 where p == null || p.Status == 1
                                 orderby (b.UpdatedAt ?? b.CreatedAt) descending
                                 select new
                                 {
                                     b.BookingId,
                                     b.BookingCode,
                                     ProviderId = b.ProviderService.Provider.UserId,
                                     ProviderName = b.ProviderService.Provider.FullName,
                                     ProviderPromptPayId = b.ProviderService.Provider.PromptPayId,
                                     Amount = (b.FinalPrice ?? b.EstimatedPrice),
                                     PayoutId = p != null ? p.PayoutId : (int?)null
                                 })
                .ToListAsync();

            return Ok(pending);
        }

        /// <summary>สร้างรายการโอน (Pending) จาก Booking ที่เสร็จงานแล้ว</summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("{id:int}/payout/init")]
        public async Task<IActionResult> InitPayout([FromRoute] int id, [FromQuery] decimal? feePercent = 10m)
        {
            var booking = await _db.Bookings
                .Include(b => b.ProviderService).ThenInclude(s => s.Provider)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null) return NotFound(new { message = "Booking not found." });

            if (booking.Status != BookingStatus.Completed)
                return BadRequest(new { message = "Booking must be Completed before payout." });

            var exists = await _db.Payouts.AsNoTracking().AnyAsync(p => p.BookingId == id);
            if (exists) return Conflict(new { message = "Payout already created for this booking." });

            var amount = booking.FinalPrice ?? booking.EstimatedPrice;
            if (amount <= 0) return BadRequest(new { message = "Invalid amount." });

            var feeRate = (feePercent ?? 10m) / 100m;
            var fee = Math.Round(amount * feeRate, 2, MidpointRounding.AwayFromZero);
            var net = amount - fee;

            var providerId = booking.ProviderService?.Provider?.UserId ?? 0;
            if (providerId <= 0) return BadRequest(new { message = "Provider not found on this booking." });

            var payout = new Payout
            {
                BookingId = booking.BookingId,
                UserId = providerId,
                GrossAmount = amount,
                FeeAmount = fee,
                NetAmount = net,
                Status = 1,
                SettledAt = null
            };

            _db.Payouts.Add(payout);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                payout.PayoutId,
                payout.BookingId,
                payout.UserId,
                payout.GrossAmount,
                payout.FeeAmount,
                payout.NetAmount,
                payout.Status,
                payout.SettledAt
            });
        }

        /// <summary>ประวัติการโอนของผู้ให้บริการ</summary>
        [HttpGet("payouts/provider/{providerId:int}")]
        public async Task<IActionResult> GetPayoutsByProvider([FromRoute] int providerId)
        {
            var list = await _db.Payouts.AsNoTracking()
                .Where(p => p.UserId == providerId)
                .OrderByDescending(p => p.SettledAt ?? p.CreatedAt)
                .Select(p => new
                {
                    p.PayoutId,
                    p.BookingId,
                    p.GrossAmount,
                    p.FeeAmount,
                    p.NetAmount,
                    p.Status,
                    p.SettledAt,
                    p.CreatedAt,
                    p.TransferSlipPath,
                    p.TransactionRef
                })  
                .ToListAsync();

            return Ok(list);
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("{id:int}/cancel")]
        public async Task<ActionResult<BookMyServiceBE.Models.Dto.CancelBookingResponse>> CancelByCustomer(
    [FromRoute] int id,
    [FromBody] BookMyServiceBE.Models.Dto.CancelBookingDto dto)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var booking = await _db.Bookings
                .Include(b => b.ProviderService)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            // ต้องเป็นเจ้าของ booking เท่านั้น
            if (booking.UserId != userId)
                return Forbid();

            // ยกเลิกซ้ำไม่ได้
            if (booking.Status == BookingStatus.CancelledByCustomer ||
                booking.Status == BookingStatus.CancelledByProvider ||
                booking.Status == BookingStatus.Cancelled)
            {
                return Conflict(new { message = "Booking already cancelled." });
            }

            // ไม่ให้ยกเลิกหลังเริ่มงาน/จบงาน
            if (booking.Status == BookingStatus.InProgress || booking.Status == BookingStatus.Completed)
                return BadRequest(new { message = $"Cannot cancel when booking is {booking.Status}." });

            // ถ้าคุณมี state machine อยู่ ให้ใช้มัน (แนะนำ)
            if (BookingStatusRules.CanTransit(booking.Status, BookingStatus.CancelledByCustomer) == false)
            {
                return BadRequest(new { message = $"Invalid transition: {booking.Status} -> {BookingStatus.CancelledByCustomer}" });
            }

            booking.Status = BookingStatus.CancelledByCustomer;
            booking.UpdatedAt = DateTime.UtcNow;

            // เก็บเหตุผล (ถ้าใน Booking ไม่มีช่อง reason ก็ข้าม)
            // ถ้าคุณอยากเก็บ reason จริง แนะนำเพิ่มคอลัมน์ CancelReason ใน Booking
            var reason = string.IsNullOrWhiteSpace(dto?.Reason) ? null : dto.Reason.Trim();

            await _db.SaveChangesAsync();

            return Ok(new BookMyServiceBE.Models.Dto.CancelBookingResponse(
                booking.BookingId,
                booking.BookingCode,
                booking.Status.ToString(),
                reason,
                booking.UpdatedAt.Value
            ));
        }

    }
}
