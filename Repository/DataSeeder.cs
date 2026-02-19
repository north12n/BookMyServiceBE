// Repository/DataSeeder.cs
using BookMyService.Models;
using BookMyServiceBE.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookMyServiceBE.Repository
{
    public class DataSeeder
    {
        private readonly ApplicationDbContext _db;
        private readonly PasswordHasher<User> _hasher = new();

        public DataSeeder(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// สร้างข้อมูลผู้ใช้เริ่มต้น (Admin, Provider, Customer) ถ้ายังไม่มี
        /// </summary>
        public async Task SeedInitialData()
        {
            // ตรวจสอบว่ามี Admin อยู่แล้วหรือไม่
            var hasAdmin = await _db.Users.AnyAsync(u => u.UserRole == UserRole.Admin);

            if (hasAdmin)
            {
                Console.WriteLine("✅ Initial data already exists. Skipping seed.");
                return;
            }

            Console.WriteLine("🌱 Seeding initial users...");

            var users = new List<User>();

            // 1. Admin Account
            var admin = new User
            {
                Email = "admin@gmail.com",
                PhoneNumber = "0812345001",
                FullName = "System Administrator",
                UserRole = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                AddressLine = "123 Admin Street",
                District = "เมือง",
                Province = "กรุงเทพมหานคร",
                PostalCode = "10100"
            };
            admin.PasswordHash = _hasher.HashPassword(admin, "admin123"); // รหัสผ่าน: admin123
            users.Add(admin);

            // 2. Provider Account (ช่างประปา)
            var provider1 = new User
            {
                Email = "p1@gmail.com",
                PhoneNumber = "0812345002",
                FullName = "ช่างประปาจอห์น",
                UserRole = UserRole.Provider,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                AddressLine = "456 Provider Road",
                District = "บางซื่อ",
                Province = "กรุงเทพมหานคร",
                PostalCode = "10800",
                BankName = "ธนาคารกรุงเทพ",
                BankAccountNumber = "1234567890",
                BankAccountName = "นายจอห์น สมิธ",
                PromptPayId = "0812345002"
            };
            provider1.PasswordHash = _hasher.HashPassword(provider1, "123456"); // รหัสผ่าน: 123456
            users.Add(provider1);

            // 3. Provider Account (ช่างไฟฟ้า)
            var provider2 = new User
            {
                Email = "p2@gmail.com",
                PhoneNumber = "0812345003",
                FullName = "ช่างไฟฟ้าแมรี่",
                UserRole = UserRole.Provider,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                AddressLine = "789 Electric Avenue",
                District = "จตุจักร",
                Province = "กรุงเทพมหานคร",
                PostalCode = "10900",
                BankName = "ธนาคารกสิกรไทย",
                BankAccountNumber = "9876543210",
                BankAccountName = "นางสาวแมรี่ จอห์นสัน",
                PromptPayId = "0812345003"
            };
            provider2.PasswordHash = _hasher.HashPassword(provider2, "123456"); // รหัสผ่าน: 123456
            users.Add(provider2);

            // 4. Customer Account
            var customer = new User
            {
                Email = "c1@gmail.com",
                PhoneNumber = "0812345004",
                FullName = "คุณลูกค้าทดสอบ",
                UserRole = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                AddressLine = "999 Customer Lane",
                District = "ลาดพร้าว",
                Province = "กรุงเทพมหานคร",
                PostalCode = "10230"
            };
            customer.PasswordHash = _hasher.HashPassword(customer, "123456"); // รหัสผ่าน: 123456
            users.Add(customer);

            // บันทึกลง Database
            await _db.Users.AddRangeAsync(users);
            await _db.SaveChangesAsync();

            Console.WriteLine("✅ Seeded users successfully:");
            Console.WriteLine($"   - Admin: admin@gmail.com / admin123");
            Console.WriteLine($"   - Provider 1: p1@gmail.com / 123456");
            Console.WriteLine($"   - Provider 2: p2@gmail.com / 123456");
            Console.WriteLine($"   - Customer: c1@gmail.com / 123456");

            // Seed SystemSettings if not exists
            await SeedSystemSettings();
        }

        /// <summary>
        /// สร้าง SystemSettings เริ่มต้น
        /// </summary>
        private async Task SeedSystemSettings()
        {
            var hasPlatformFee = await _db.SystemSettings
                .AnyAsync(s => s.Key == "PLATFORM_FEE_PERCENT");

            if (hasPlatformFee)
            {
                Console.WriteLine("✅ SystemSettings already exist. Skipping.");
                return;
            }

            Console.WriteLine("🌱 Seeding SystemSettings...");

            var settings = new List<SystemSetting>
            {
                new SystemSetting
                {
                    Key = "PROMPTPAY_ID",
                    Value = "0953962087",
                },
                new SystemSetting
                {
                    Key = "PLATFORM_FEE_PERCENT",
                    Value = "5.00",
                }
            };

            await _db.SystemSettings.AddRangeAsync(settings);
            await _db.SaveChangesAsync();

            Console.WriteLine("✅ Seeded SystemSettings successfully");
        }
    }
}
