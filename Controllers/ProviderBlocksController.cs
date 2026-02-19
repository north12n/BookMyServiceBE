using BookMyService.Models;
using BookMyServiceBE.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/provider/blocks")]
    [Produces("application/json")]
    [Authorize(Roles = "Provider")]
    public class ProviderBlocksController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ProviderBlocksController(ApplicationDbContext db)
        {
            _db = db;
        }

        public record BlockDto(int BlockId, DateTime Start, DateTime End, string? Reason);

        public record CreateBlockRequest(DateTime Start, DateTime End, string? Reason);

        [HttpGet]
        public async Task<ActionResult<List<BlockDto>>> Get([FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            if (to <= from) return BadRequest(new { message = "`to` must be greater than `from`." });

            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var blocks = await _db.ProviderAvailabilityBlocks.AsNoTracking()
                .Where(b => b.ProviderId == providerId)
                .Where(b => b.EndUtc > from && b.StartUtc < to) // overlap
                .OrderBy(b => b.StartUtc)
                .Select(b => new BlockDto(b.BlockId, b.StartUtc, b.EndUtc, b.Reason))
                .ToListAsync();

            return Ok(blocks);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBlockRequest req)
        {
            if (req.End <= req.Start) return BadRequest(new { message = "`End` must be greater than `Start`." });

            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            // กัน block ชนกันเอง (optional)
            var overlap = await _db.ProviderAvailabilityBlocks.AsNoTracking()
                .AnyAsync(b => b.ProviderId == providerId && b.EndUtc > req.Start && b.StartUtc < req.End);

            if (overlap) return Conflict(new { message = "Block overlaps with existing block." });

            var block = new ProviderAvailabilityBlock
            {
                ProviderId = providerId,
                StartUtc = req.Start,
                EndUtc = req.End,
                Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.ProviderAvailabilityBlocks.Add(block);
            await _db.SaveChangesAsync();

            return Ok(new { block.BlockId });
        }

        [HttpDelete("{blockId:int}")]
        public async Task<IActionResult> Delete([FromRoute] int blockId)
        {
            var providerId = User.GetUserId();
            if (providerId <= 0) return Unauthorized();

            var block = await _db.ProviderAvailabilityBlocks.FirstOrDefaultAsync(b => b.BlockId == blockId);
            if (block is null) return NotFound();
            if (block.ProviderId != providerId) return Forbid();

            _db.ProviderAvailabilityBlocks.Remove(block);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
