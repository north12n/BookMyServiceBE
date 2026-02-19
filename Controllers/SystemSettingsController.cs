using BookMyService.Models;
using BookMyServiceBE.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/system")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public sealed class SystemSettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public SystemSettingsController(ApplicationDbContext db) => _db = db;

        public sealed record UpdatePromptPayRequest(string PromptPayId);

        [HttpPost("promptpay")]
        public async Task<IActionResult> UpdatePromptPay([FromBody] UpdatePromptPayRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.PromptPayId))
                return BadRequest(new { message = "PromptPayId is required." });

            var digits = new string(req.PromptPayId.Where(char.IsDigit).ToArray());
            if (digits.Length != 10 && digits.Length != 13)
                return BadRequest(new { message = "PromptPay must be 10-digit mobile or 13-digit national/tax id." });

            var setting = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == "PROMPTPAY_ID");
            if (setting is null)
            {
                setting = new SystemSetting { Key = "PROMPTPAY_ID", Value = digits };
                _db.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = digits;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "PromptPay updated.", promptPayId = digits });
        }

        [HttpGet("promptpay")]
        public async Task<IActionResult> GetPromptPay()
        {
            var setting = await _db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "PROMPTPAY_ID");

            if (setting is null)
                return BadRequest(new { message = "PromptPay ID not configured." });

            var promptPayId = setting.Value;


            return Ok(new { promptPayId = setting?.Value });
        }

        // =========================
        // GET /api/admin/system/settings
        // รายการ settings ทั้งหมด
        // =========================
        [HttpGet("settings")]
        public async Task<IActionResult> GetAllSettings()
        {
            var settings = await _db.SystemSettings
                .AsNoTracking()
                .OrderBy(s => s.Key)
                .Select(s => new { s.SystemSettingId, s.Key, s.Value, s.UpdatedAt })
                .ToListAsync();

            return Ok(settings);
        }

        // =========================
        // GET /api/admin/system/settings/{key}
        // ดึง setting ตาม key
        // =========================
        [HttpGet("settings/{key}")]
        public async Task<IActionResult> GetSettingByKey([FromRoute] string key)
        {
            var setting = await _db.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting is null)
                return NotFound(new { message = $"Setting with key '{key}' not found." });

            return Ok(new { setting.SystemSettingId, setting.Key, setting.Value, setting.UpdatedAt });
        }

        // =========================
        // PUT /api/admin/system/settings/{key}
        // อัพเดต setting ตาม key (สร้างใหม่ถ้ายังไม่มี)
        // =========================
        public sealed record UpdateSettingRequest(string Value);

        [HttpPut("settings/{key}")]
        public async Task<IActionResult> UpsertSetting([FromRoute] string key, [FromBody] UpdateSettingRequest req)
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { message = "Key is required." });

            if (string.IsNullOrWhiteSpace(req.Value))
                return BadRequest(new { message = "Value is required." });

            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting is null)
            {
                setting = new SystemSetting
                {
                    Key = key,
                    Value = req.Value,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = req.Value;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Setting updated.",
                setting.SystemSettingId,
                setting.Key,
                setting.Value,
                setting.UpdatedAt
            });
        }

        // =========================
        // DELETE /api/admin/system/settings/{key}
        // ลบ setting ตาม key
        // =========================
        [HttpDelete("settings/{key}")]
        public async Task<IActionResult> DeleteSetting([FromRoute] string key)
        {
            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting is null)
                return NotFound(new { message = $"Setting with key '{key}' not found." });

            _db.SystemSettings.Remove(setting);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Setting deleted.", key });
        }

        // =========================
        // POST /api/admin/system/settings/init-defaults
        // สร้าง default settings (ถ้ายังไม่มี)
        // =========================
        [HttpPost("settings/init-defaults")]
        public async Task<IActionResult> InitDefaultSettings()
        {
            var defaults = new Dictionary<string, string>
            {
                { "PLATFORM_FEE_PERCENT", "10" },
                { "REFUND_POLICY_BEFORE_ASSIGNED", "100" },
                { "REFUND_POLICY_AFTER_ASSIGNED", "50" },
                { "REFUND_POLICY_IN_PROGRESS", "0" }
            };

            var existingKeys = await _db.SystemSettings
                .Where(s => defaults.Keys.Contains(s.Key))
                .Select(s => s.Key)
                .ToListAsync();

            var toAdd = defaults
                .Where(kvp => !existingKeys.Contains(kvp.Key))
                .Select(kvp => new SystemSetting
                {
                    Key = kvp.Key,
                    Value = kvp.Value,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            if (toAdd.Any())
            {
                _db.SystemSettings.AddRange(toAdd);
                await _db.SaveChangesAsync();
            }

            return Ok(new
            {
                message = "Default settings initialized.",
                added = toAdd.Count,
                keys = toAdd.Select(s => s.Key).ToList()
            });
        }
    }
}
