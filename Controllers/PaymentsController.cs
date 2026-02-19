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
    [Route("api/payments")]
    [Produces("application/json")]
    public sealed class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileUpload _fileUpload;

        public PaymentsController(ApplicationDbContext db, IFileUpload fileUpload)
        {
            _db = db;
            _fileUpload = fileUpload;
        }
        public sealed class UploadSlipForm
        {
            public IFormFile File { get; set; } = null!;
        }

        // =========================
        // ADMIN: List payments
        // GET /api/payments
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentListItemDto>>> ListPayments()
        {
            var list = await _db.Payments.AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentListItemDto(
                    p.PaymentId,
                    p.BookingId,
                    p.UserId,
                    p.Amount,
                    p.Status.ToString(),
                    p.SlipPath,
                    p.CreatedAt,
                    p.PaidAt
                ))
                .ToListAsync();

            return Ok(list);
        }

        // =========================
        // CUSTOMER/ADMIN: Get payment by booking
        // GET /api/payments/booking/{bookingId:int}
        // =========================
        [Authorize]
        [HttpGet("booking/{bookingId:int}")]
        public async Task<ActionResult<PaymentListItemDto>> GetByBooking([FromRoute] int bookingId)
        {
            var payment = await _db.Payments.AsNoTracking()
                .Where(p => p.BookingId == bookingId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentListItemDto(
                    p.PaymentId,
                    p.BookingId,
                    p.UserId,
                    p.Amount,
                    p.Status.ToString(),
                    p.SlipPath,
                    p.CreatedAt,
                    p.PaidAt
                ))
                .FirstOrDefaultAsync();

            if (payment is null) return NotFound(new { message = "Payment not found." });

            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();
            if (!User.IsInRole("Admin") && payment.UserId != userId) return Forbid();

            return Ok(payment);
        }

        // =========================
        // CUSTOMER/ADMIN: Init payment by booking
        // POST /api/payments/booking/{bookingId:int}/init
        // =========================
        [Authorize]
        [HttpPost("booking/{bookingId:int}/init")]
        public async Task<ActionResult<PaymentListItemDto>> InitPayment([FromRoute] int bookingId)
        {
            var booking = await _db.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking is null) return NotFound(new { message = "Booking not found." });

            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();
            if (!User.IsInRole("Admin") && booking.UserId != userId) return Forbid();

            var existing = await _db.Payments.AsNoTracking()
                .Where(p => p.BookingId == bookingId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                return Ok(new PaymentListItemDto(
                    existing.PaymentId,
                    existing.BookingId,
                    existing.UserId,
                    existing.Amount,
                    existing.Status.ToString(),
                    existing.SlipPath,
                    existing.CreatedAt,
                    existing.PaidAt
                ));
            }

            var amount = booking.FinalPrice ?? booking.EstimatedPrice;
            if (amount <= 0) return BadRequest(new { message = "Invalid amount." });

            var payment = new Payment
            {
                BookingId = booking.BookingId,
                UserId = booking.UserId,
                Amount = amount,
                PaymentType = PaymentType.Full,
                PaymentMethod = PaymentMethod.PromptPay,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return Ok(new PaymentListItemDto(
                payment.PaymentId,
                payment.BookingId,
                payment.UserId,
                payment.Amount,
                payment.Status.ToString(),
                payment.SlipPath,
                payment.CreatedAt,
                payment.PaidAt
            ));
        }

        // =========================
        // CUSTOMER: Upload slip to payment
        // POST /api/payments/{paymentId:int}/slip
        // =========================
        [Authorize(Roles = "Customer")]
        [HttpPost("{paymentId:int}/slip")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadSlip([FromRoute] int paymentId, [FromForm] UploadSlipForm form)
        {
            var userId = User.GetUserId();
            if (userId <= 0) return Unauthorized();

            var file = form.File;
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Slip file is required." });

            var payment = await _db.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment is null) return NotFound(new { message = "Payment not found." });

            if (payment.UserId != userId) return Forbid();

            if (payment.Status == PaymentStatus.Paid)
                return Conflict(new { message = "Payment already paid." });

            var paths = await _fileUpload.UploadFilesAsync(new List<IFormFile> { file }, $"payment-{paymentId}");
            var newPath = paths.First();

            if (!string.IsNullOrWhiteSpace(payment.SlipPath))
                _fileUpload.DeleteFile(payment.SlipPath);

            payment.SlipPath = newPath;
            payment.Status = PaymentStatus.SlipUploaded;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Slip uploaded.",
                paymentId = payment.PaymentId,
                slipPath = payment.SlipPath,
                status = payment.Status.ToString()
            });
        }

        // =========================
        // ADMIN: Mark payment as paid (confirm slip)
        // PATCH /api/payments/{paymentId:int}/mark-paid
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPatch("{paymentId:int}/mark-paid")]
        public async Task<IActionResult> MarkPaid([FromRoute] int paymentId)
        {
            var payment = await _db.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment is null) return NotFound(new { message = "Payment not found." });

            // idempotent
            if (payment.Status == PaymentStatus.Paid) return NoContent();

            // ต้องมีสลิปก่อน
            if (string.IsNullOrWhiteSpace(payment.SlipPath))
                return BadRequest(new { message = "Cannot mark paid without slip." });

            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;

            // sync booking: PendingPayment -> Paid
            if (payment.Booking is not null && payment.Booking.Status == BookingStatus.PendingPayment)
            {
                payment.Booking.Status = BookingStatus.Paid;
                payment.Booking.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // =========================
        // ADMIN: Reject payment slip
        // PATCH /api/payments/{paymentId:int}/reject
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPatch("{paymentId:int}/reject")]
        public async Task<IActionResult> RejectSlip([FromRoute] int paymentId, [FromBody] RejectSlipDto dto)
        {
            var payment = await _db.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment is null) return NotFound(new { message = "Payment not found." });

            if (payment.Status == PaymentStatus.Paid)
                return BadRequest(new { message = "Cannot reject paid payment." });

            payment.Status = PaymentStatus.Rejected;

            // TODO: อาจเพิ่ม field RejectionReason ใน Payment model ถ้าต้องการ
            // payment.RejectionReason = dto.Reason;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        public sealed class RejectSlipDto
        {
            public string? Reason { get; set; }
        }

        // =========================
        // ADMIN: Approve refund (คืนเงินลูกค้า)
        // PATCH /api/payments/{paymentId:int}/approve-refund
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPatch("{paymentId:int}/approve-refund")]
        public async Task<IActionResult> ApproveRefund([FromRoute] int paymentId)
        {
            var payment = await _db.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment is null) return NotFound(new { message = "Payment not found." });

            var booking = payment.Booking;
            if (booking is null) return NotFound(new { message = "Booking not found." });

            // ต้องยกเลิกแล้ว และมี RefundAmount
            if (booking.Status != BookingStatus.CancelledByCustomer &&
                booking.Status != BookingStatus.CancelledByProvider)
            {
                return BadRequest(new { message = "Booking is not cancelled." });
            }

            if (booking.RefundAmount is null || booking.RefundAmount <= 0)
                return BadRequest(new { message = "No refund amount to process." });

            // Mark payment as refunded
            payment.Status = PaymentStatus.Refunded;

            // TODO: ส่วนนี้ควรมี integration กับระบบจ่ายเงินจริง (e.g., PromptPay API)
            // สำหรับ mock ให้ถือว่าโอนคืนเสร็จแล้ว

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Refund approved",
                paymentId = payment.PaymentId,
                bookingId = booking.BookingId,
                refundAmount = booking.RefundAmount,
                refundPercentage = booking.RefundPercentage
            });
        }


        // =========================
        // ADMIN: Pay payout to provider + upload transfer slip
        // PATCH /api/payments/payouts/{payoutId:int}/pay
        // =========================
        public sealed class PayPayoutForm
        {
            public string? TransactionRef { get; set; }
            public IFormFile SlipFile { get; set; } = null!;
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("payouts/{payoutId:int}/pay")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PayPayout([FromRoute] int payoutId, [FromForm] PayPayoutForm form)
        {
            var payout = await _db.Payouts
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.PayoutId == payoutId);

            if (payout is null) return NotFound(new { message = "Payout not found." });

            // idempotent
            if (payout.Status == 2) return NoContent();

            // ต้อง Completed ก่อน
            if (payout.Booking.Status != BookingStatus.Completed)
                return BadRequest(new { message = "Booking must be Completed before payout." });

            if (form.SlipFile is null || form.SlipFile.Length == 0)
                return BadRequest(new { message = "SlipFile is required." });

            var paths = await _fileUpload.UploadFilesAsync(new List<IFormFile> { form.SlipFile }, $"payout-{payoutId}");
            var newPath = paths.First();

            if (!string.IsNullOrWhiteSpace(payout.TransferSlipPath))
                _fileUpload.DeleteFile(payout.TransferSlipPath);

            payout.TransferSlipPath = newPath;
            payout.TransactionRef = string.IsNullOrWhiteSpace(form.TransactionRef) ? null : form.TransactionRef.Trim();

            payout.Status = 2; // Paid
            payout.SettledAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
