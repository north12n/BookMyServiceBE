using BookMyServiceBE.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using System.Linq.Expressions;
using System.Text.Json;
using System.Linq;
using System.Reflection.Emit;

namespace BookMyService.Models
{
    public class ApplicationDbContext : DbContext
    {
        // ctor สำหรับ DI (ใช้กับ Program.cs -> AddDbContext)
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // เผื่อกรณี dotnet ef ใช้ Context โดยไม่มี Program.cs
        // ถ้าใช้ appsettings ผ่าน DI อย่างเดียวก็ไม่จำเป็นต้องมีเมธอดนี้ก็ได้
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // เปลี่ยน connection string ให้ตรงกับเครื่อง/เซิร์ฟเวอร์ของคุณ
                optionsBuilder.UseSqlServer(

                   "Server=10.103.0.16,1433; Database=DB_BookMyService; Trusted_Connection=false; MultipleActiveResultSets=true; TrustServerCertificate=True; User Id=student; Password=Cs@2700; Encrypt=false;"
                   
                   );
            }
        }
        //"Server=LAPTOP-EGR1BSET\\SQLEXPRESS;Database=BookMyServiceDb1;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"

        // server|    "Server=10.103.0.16,1433; Database=DB_BookMyService; Trusted_Connection=false; MultipleActiveResultSets=true; TrustServerCertificate=True; User Id=student; Password=Cs@2700; Encrypt=false;"

        // DbSet ต่างๆ ของระบบ
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ServiceCategory> ServiceCategories { get; set; } = null!;
        public DbSet<ProviderService> ProviderServices { get; set; } = null!;
        public DbSet<Booking> Bookings { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<WebhookLog> WebhookLogs { get; set; } = null!;
        public DbSet<WorkLog> WorkLogs { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<Payout> Payouts { get; set; } = null!;
        public DbSet<Complaint> Complaints { get; set; } = null!;
        public DbSet<ProviderAvailabilityBlock> ProviderAvailabilityBlocks { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<ProviderAvailabilityBlock>(e =>
            {
                e.HasKey(x => x.BlockId);

                e.HasOne(x => x.Provider)
                    .WithMany(u => u.AvailabilityBlocks)
                    .HasForeignKey(x => x.ProviderId)
                    .OnDelete(DeleteBehavior.Restrict);

                // แนะนำ index เพื่อให้ query ช่วงเวลาวิ่งไว
                e.HasIndex(x => new { x.ProviderId, x.StartUtc, x.EndUtc });
            });
            // Users
            b.Entity<User>(e =>
            {
                e.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
                e.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
            });

            // ServiceCategory
            b.Entity<ServiceCategory>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);
            });

            // ProviderService
            b.Entity<ProviderService>(e =>
            {
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.Property(x => x.Description).HasMaxLength(1000);
                e.Property(x => x.UnitLabel).HasMaxLength(50);
                e.Property(x => x.BasePrice).HasPrecision(10, 2);

                e.HasOne(x => x.Provider)
                    .WithMany(x => x.ProviderServices)
                    .HasForeignKey(x => x.ProviderId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ServiceCategory)
                    .WithMany(x => x.ProviderServices)
                    .HasForeignKey(x => x.ServiceCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // NEW: Map List<string> Images → nvarchar(max) JSON (ใช้ Func<> แทน Expression<>)
            var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

            var imagesEquals = new Func<List<string>?, List<string>?, bool>((a, b) =>
            {
                if (ReferenceEquals(a, b)) return true;
                if (a is null || b is null) return false;
                return a.SequenceEqual(b);
            });

            var imagesHash = new Func<List<string>?, int>(c =>
            {
                if (c is null) return 0;
                unchecked
                {
                    int acc = 19; // คงที่เริ่ม
                    foreach (var v in c)
                    {
                        acc = HashCode.Combine(acc, v?.GetHashCode() ?? 0);
                    }
                    return acc;
                }
            });

            var imagesSnapshot = new Func<List<string>?, List<string>>(c =>
                c is null ? new List<string>() : new List<string>(c));

            var listComparer = new ValueComparer<List<string>>(
                (a, b) => imagesEquals(a, b),
                c => imagesHash(c),
                c => imagesSnapshot(c)
            );

            b.Entity<ProviderService>()
             .Property(p => p.Images)
             .HasConversion(
                 v => JsonSerializer.Serialize(v ?? new List<string>(), jsonOpts),
                 v => string.IsNullOrWhiteSpace(v)
                        ? new List<string>()
                        : (JsonSerializer.Deserialize<List<string>>(v, jsonOpts) ?? new List<string>())
             )
             .HasColumnType("nvarchar(max)")
             .HasColumnName("Images")
             .Metadata.SetValueComparer(listComparer);

            // Booking
            b.Entity<Booking>(e =>
            {
                e.Property(x => x.BookingCode).HasMaxLength(50).IsRequired();
                e.HasIndex(x => x.BookingCode).IsUnique();

                e.Property(x => x.JobTitle).HasMaxLength(200).IsRequired();
                e.Property(x => x.JobDescription).HasMaxLength(1000);
                e.Property(x => x.AddressLine).HasMaxLength(300).IsRequired();
                e.Property(x => x.District).HasMaxLength(100).IsRequired();
                e.Property(x => x.Province).HasMaxLength(100).IsRequired();
                e.Property(x => x.PostalCode).HasMaxLength(10).IsRequired();

                e.Property(x => x.EstimatedPrice).HasPrecision(10, 2);
                e.Property(x => x.FinalPrice).HasPrecision(10, 2);

                e.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ProviderService)
                    .WithMany(x => x.Bookings)
                    .HasForeignKey(x => x.ProviderServiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                // 1:1 กับ WorkLog / Review / Payout
                e.HasOne(x => x.WorkLog)
                    .WithOne(x => x.Booking)
                    .HasForeignKey<WorkLog>(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Review)
                    .WithOne(x => x.Booking)
                    .HasForeignKey<Review>(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Payout)
                    .WithOne(x => x.Booking)
                    .HasForeignKey<Payout>(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Payment
            b.Entity<Payment>(e =>
            {
                e.Property(x => x.Amount).HasPrecision(10, 2);

                e.HasOne(x => x.Booking)
                    .WithMany(x => x.Payments)
                    .HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Payer)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // WebhookLog
            b.Entity<WebhookLog>(e =>
            {
                e.HasOne(x => x.Payment)
                    .WithMany(x => x.WebhookLogs)
                    .HasForeignKey(x => x.PaymentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // WorkLog (1:1 Booking)
            b.Entity<WorkLog>(e =>
            {
                e.HasIndex(x => x.BookingId).IsUnique();
                e.HasOne(x => x.Provider)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Review (1:1 Booking)
            b.Entity<Review>(e =>
            {
                e.HasIndex(x => x.BookingId).IsUnique();
                e.Property(x => x.Comment).HasMaxLength(1000);
                e.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5"));

                e.HasOne(x => x.Reviewer)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Payout (1:1 Booking)
            b.Entity<Payout>(e =>
            {
                // 1) บังคับให้ BookingId ไม่ซ้ำ -> หนึ่ง Booking มีได้แค่หนึ่ง Payout (1:1)
                e.HasIndex(x => x.BookingId).IsUnique();

                // 2) กำหนดชนิด/ความละเอียดของเงินใน DB เป็น decimal(10,2)
                e.Property(x => x.GrossAmount).HasPrecision(10, 2);
                e.Property(x => x.FeeAmount).HasPrecision(10, 2);
                e.Property(x => x.NetAmount).HasPrecision(10, 2);

                // 3) ตั้งความสัมพันธ์กับ Provider (User):
                //    - Payout หนึ่งอัน มีผู้ให้บริการ (User) หนึ่งคน (FK = UserId)
                //    - WithMany() = ฝั่ง User ไม่ได้ประกาศ navigation collection
                //    - Restrict = ห้ามลบ User ถ้ายังมี Payout ชี้อยู่ (ไม่ cascade delete)
                e.HasOne(x => x.Provider)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            }); 

            // Complaint
            b.Entity<Complaint>(e =>
            {
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();

                e.HasOne(x => x.Booking)
                    .WithMany(x => x.Complaints)
                    .HasForeignKey(x => x.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Creator)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ChatMessage
            b.Entity<ChatMessage>(e =>
            {
                e.HasOne(x => x.Sender)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Booking)
                    .WithMany()
                    .HasForeignKey(x => x.RelatedBookingId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // SystemSetting
            b.Entity<SystemSetting>(e =>
            {
                e.HasIndex(x => x.Key).IsUnique();
                e.Property(x => x.Key).HasMaxLength(100).IsRequired();
                e.Property(x => x.Value).HasMaxLength(400).IsRequired();
            });

        }
    }

    
}