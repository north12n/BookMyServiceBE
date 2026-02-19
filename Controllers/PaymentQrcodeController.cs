using BookMyService.Models;
using BookMyServiceBE.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookMyServiceBE.Controllers
{
    [ApiController]
    [Route("api/PaymentQrcode")]
    [Produces("application/json")]
    public sealed class PaymentQrcodeController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public PaymentQrcodeController(ApplicationDbContext db) => _db = db;

        /// <summary>
        /// GET /api/PaymentQrcode/qrcode?amount=250.00&ref=BK-20260214-123
        /// คืนเป็นรูป PNG ของ QR Code
        /// อ่าน PromptPay ID จาก SystemSettings (Key: PROMPTPAY_ID)
        /// </summary>
        [HttpGet("qrcode")]
        [Produces("image/png")]
        public async Task<IActionResult> GetPromptPayQr([FromQuery] decimal amount, [FromQuery] string? @ref = null)
        {
            if (amount <= 0)
                return BadRequest(new { message = "Amount must be greater than zero." });

            // อ่าน PromptPay ID จาก SystemSettings
            var promptPaySetting = await _db.SystemSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "PROMPTPAY_ID");

            if (promptPaySetting is null || string.IsNullOrWhiteSpace(promptPaySetting.Value))
                return BadRequest(new { message = "PromptPay ID not configured. Please contact admin." });

            var payload = PromptPayEmvPayload.Generate(promptPaySetting.Value, amount, @ref);

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var pngBytes = qrCode.GetGraphic(20);

            return File(pngBytes, "image/png");
        }
    }

    /// <summary>
    /// PromptPay EMVCo Merchant-Presented Payload Generator
    /// - รองรับ Mobile (10 หลัก), National ID/Tax ID (13 หลัก)
    /// - ใส่ Amount (Tag 54)
    /// - ใส่ Reference ใน Tag 62 (Additional Data Field Template)
    /// - ใส่ CRC16 (Tag 63)
    /// </summary>
    internal static class PromptPayEmvPayload
    {
        // EMV Tags
        private const string TAG_PAYLOAD_FORMAT = "00";
        private const string TAG_POI_METHOD = "01";
        private const string TAG_MERCHANT_INFO = "29"; // PromptPay
        private const string TAG_CURRENCY = "53";
        private const string TAG_AMOUNT = "54";
        private const string TAG_COUNTRY = "58";
        private const string TAG_ADDITIONAL = "62";
        private const string TAG_CRC = "63";

        // Merchant Info sub-tags
        private const string SUB_AID = "00";
        private const string SUB_PROMPTPAY_ID = "01";

        // Additional Data sub-tags
        private const string SUB_REFERENCE = "05"; // ใช้เป็น reference (ไม่บังคับธนาคารทุกเจ้าจะแสดง แต่เก็บใน payload ได้)

        // Static values
        private const string PAYLOAD_FORMAT = "01";
        private const string POI_DYNAMIC = "12"; // ✅ Dynamic QR (เหมาะกับมี amount)
        private const string AID_PROMPTPAY = "A000000677010111";
        private const string THB = "764";
        private const string COUNTRY_TH = "TH";

        public static string Generate(string promptPayId, decimal amount, string? reference)
        {
            var sb = new StringBuilder();

            // 00 Payload Format
            sb.Append(Tlv(TAG_PAYLOAD_FORMAT, PAYLOAD_FORMAT));

            // 01 POI Method (Dynamic)
            sb.Append(Tlv(TAG_POI_METHOD, POI_DYNAMIC));

            // 29 Merchant Info (PromptPay)
            var billerId = FormatPromptPayId(promptPayId);
            var merchantInfo = new StringBuilder();
            merchantInfo.Append(Tlv(SUB_AID, AID_PROMPTPAY));
            merchantInfo.Append(Tlv(SUB_PROMPTPAY_ID, billerId));
            sb.Append(Tlv(TAG_MERCHANT_INFO, merchantInfo.ToString()));

            // 53 Currency
            sb.Append(Tlv(TAG_CURRENCY, THB));

            // 54 Amount (2 decimals)
            var amountStr = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            sb.Append(Tlv(TAG_AMOUNT, amountStr));

            // 58 Country
            sb.Append(Tlv(TAG_COUNTRY, COUNTRY_TH));

            // 62 Additional Data (Reference)
            if (!string.IsNullOrWhiteSpace(reference))
            {
                // จำกัดความยาว reference เผื่อบาง bank strict
                var refClean = reference.Trim();
                if (refClean.Length > 25) refClean = refClean.Substring(0, 25);

                var additional = new StringBuilder();
                additional.Append(Tlv(SUB_REFERENCE, refClean));
                sb.Append(Tlv(TAG_ADDITIONAL, additional.ToString()));
            }

            // 63 CRC (append "6304" then calculate CRC over entire string)
            var withoutCrc = sb.ToString() + TAG_CRC + "04";
            var crc = Crc16Ccitt(withoutCrc);
            return withoutCrc + crc;
        }

        private static string Tlv(string tag, string value)
        {
            var len = value.Length.ToString("D2");
            return tag + len + value;
        }

        private static string FormatPromptPayId(string promptPayId)
        {
            var digits = new string(promptPayId.Where(char.IsDigit).ToArray());

            // Mobile number 10 digits: 0XXXXXXXXX -> 0066XXXXXXXXX (remove leading 0)
            if (digits.Length == 10)
            {
                if (!digits.StartsWith("0"))
                    throw new ArgumentException("Invalid mobile number format for PromptPay.");
                return "0066" + digits.Substring(1);
            }

            // National ID / Tax ID 13 digits
            if (digits.Length == 13)
            {
                return digits;
            }

            throw new ArgumentException("PromptPay ID must be a 10-digit mobile or 13-digit national/tax ID.");
        }

        // CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF)
        private static string Crc16Ccitt(string input)
        {
            ushort crc = 0xFFFF;
            foreach (var ch in input)
            {
                crc ^= (ushort)(ch << 8);
                for (int i = 0; i < 8; i++)
                {
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ 0x1021)
                        : (ushort)(crc << 1);
                }
            }
            return crc.ToString("X4");
        }
    }
}
