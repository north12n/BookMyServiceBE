# บทที่ 2: วัตถุประสงค์ที่สำคัญในการพัฒนาระบบจองบริการ

## บทนำ

ในการพัฒนาแพลตฟอร์มจองบริการออนไลน์ (BookMyService) สำหรับอำเภอเมืองจังหวัดกาญจนบุรี จำเป็นต้องศึกษาและนำไปใช้เทคโนโลยีและแนวปฏิบัติที่ดีที่สุด (Best Practices) จากภาควิชาการและงานวิจัยที่เกี่ยวข้อง เพื่อสร้างรากฐานที่มั่นคงในการตัดสินใจเลือกเทคโนโลยี และออกแบบสถาปัตยกรรมระบบให้เหมาะสมกับบริบทและข้อจำกัดของโครงการ

### ด้านที่ศึกษาครอบคลุม 6 ส่วนหลัก

| # | ด้าน | วัตถุประสงค์ | เทคโนโลยี |
|---|------|----------|---------|
| 1 | **โปรแกรมเชิงวัตถุ (OOP)** | โครงสร้างโค้ดยืดหยุ่นและบำรุงรักษาง่าย | C# |
| 2 | **ฐานข้อมูล** | จัดการข้อมูลและรักษาสมบูรณ์ | SQL Server |
| 3 | **Frontend** | ส่วนติดต่อผู้ใช้ที่ตอบสนองเร็ว | React + TypeScript |
| 4 | **Backend API** | API ที่มีประสิทธิภาพสูง | ASP.NET Core |
| 5 | **State Management** | จัดการสถานะแบบรวมศูนย์ | Redux Toolkit |
| 6 | **วิเคราะห์สถิติ** | ประเมินและวิเคราะห์คุณภาพระบบ | Statistical Analysis |

---

## 1. การเขียนโปรแกรมเชิงวัตถุ (Object-Oriented Programming)

### 1.1 บทบาทและความสำคัญ

**นิยาม**: การเขียนโปรแกรมเชิงวัตถุ (OOP) คือกระบวนทัศน์ที่มุ่งเน้นการจัดการโครงสร้างข้อมูลในลักษณะของวัตถุ (Objects) โดยแต่ละวัตถุประกอบด้วย:
- **สถานะ (State)**: ข้อมูลที่เก็บไว้
- **พฤติกรรม (Behavior)**: การทำงานผ่านเมธอด

**ประโยชน์จากการศึกษาก่อนหน้า**:
- ลดข้อผิดพลาดในการพัฒนา **40%** (Gamma et al., 1994)
- เพิ่มความเร็วในการพัฒนา **30%** 
- หลักการ SOLID ช่วยลดต้นทุนบำรุงรักษาระยะยาวอย่างมีนัยสำคัญ (Martin, 2008)

### 1.2 องค์ประกอบหลัก 5 ส่วน

```mermaid
graph TB
    A["🏗️ คลาส<br/>(Class)"]
    B["⚙️ เมธอด<br/>(Method)"]
    C["👪 การสืบทอด<br/>(Inheritance)"]
    D["📋 อินเตอร์เฟซ<br/>(Interface)"]
    E["🔧 คอนสตรัคเตอร์<br/>(Constructor)"]
    
    A --> |สร้าง| B
    A --> |ใช้| C
    A --> |นำมาใช้| D
    A --> |สร้างจาก| E
    
    style A fill:#e1f5ff
    style B fill:#f3e5f5
    style C fill:#e8f5e9
    style D fill:#fff3e0
    style E fill:#fce4ec
```

---

## 1.3 อินเตอร์เฟซ (Interface): กำหนดสัญญาของระบบ

### ลักษณะพื้นฐาน

อินเตอร์เฟซทำหน้าที่เป็น **"สัญญา"** ที่กำหนดว่าคลาสต้องนำไปพัฒนา (Implement) โดยไม่ระบุรายละเอียดการทำงาน ช่วยให้:
- ✅ สร้างมาตรฐานการทำงาน
- ✅ เพิ่มความยืดหยุ่นในการออกแบบ
- ✅ ใช้ Polymorphism ได้อย่างเต็มที่

### ตัวอย่างจากระบบ BookMyService

```csharp
// 📋 อินเตอร์เฟซ: กำหนดข้อตกลง
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

// ✅ คลาส: นำเมธอดมาพัฒนา
public class Repository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _db;
    private readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext db)
    {
        _db = db;
        _dbSet = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null) return false;
        _dbSet.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _dbSet.FindAsync(id) != null;
    }
}

// 🎯 การใช้งาน: ผ่าน Dependency Injection
[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IRepository<Booking> _bookingRepo;

    public BookingsController(IRepository<Booking> bookingRepo)
    {
        _bookingRepo = bookingRepo;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(int id)
    {
        var booking = await _bookingRepo.GetByIdAsync(id);
        if (booking == null)
            return NotFound(new { message = "ไม่พบการจองนี้" });
        
        return Ok(booking);
    }
}
```

**🎁 ประโยชน์ของการออกแบบนี้**:
- 👉 ผ่าน Interface แทน Class เพื่อเพิ่มความยืดหยุ่น
- 👉 สามารถสลับ Repository Implementation ได้ง่าย (เช่น จาก SQL Server → PostgreSQL)
- 👉 ง่ายในการทำ Unit Testing ด้วย Mock Objects

---

## 1.4 คอนสตรัคเตอร์ (Constructor): กำหนดค่าเริ่มต้นอย่างปลอดภัย

### ลักษณะพื้นฐาน

คอนสตรัคเตอร์เป็นเมธอดพิเศษที่ทำงานอัตโนมัติเมื่อสร้างวัตถุใหม่ โดยมีหน้าที่:
- ✅ กำหนดค่าเริ่มต้นให้ตัวแปร
- ✅ รับประกันว่าวัตถุอยู่ในสถานะที่ถูกต้องเสมอ
- ✅ ป้องกัน "Invalid State" ที่อาจเกิดจากการลืมกำหนดค่า

### ตัวอย่างจากระบบ: คลาส Booking

```csharp
public class Booking
{
    // Identity & Relationships
    public int BookingId { get; set; }
    [MaxLength(50)]
    public string BookingCode { get; set; } = null!;
    public int UserId { get; set; }
    public int ProviderServiceId { get; set; }

    // Job Details
    [MaxLength(200)]
    public string JobTitle { get; set; } = null!;
    [MaxLength(1000)]
    public string? JobDescription { get; set; }
    public DateTime RequestedStartAt { get; set; }
    public DateTime? RequestedEndAt { get; set; }

    // Address Information (ที่อยู่ที่ต้องการให้บริการ)
    [MaxLength(300)]
    public string AddressLine { get; set; } = null!;
    [MaxLength(100)]
    public string District { get; set; } = null!;
    [MaxLength(100)]
    public string Province { get; set; } = null!;
    [MaxLength(10)]
    public string PostalCode { get; set; } = null!;

    // Pricing
    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedPrice { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal? FinalPrice { get; set; }

    // Cancellation & Refund
    [MaxLength(500)]
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal? RefundAmount { get; set; }

    // Status & Timestamps
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation Properties
    public User Customer { get; set; } = null!;
    public ProviderService ProviderService { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public WorkLog? WorkLog { get; set; }
    public Review? Review { get; set; }

    // 🔧 Default Constructor
    public Booking()
    {
        BookingCode = GenerateBookingCode();
        Status = BookingStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    // 🔧 Parameterized Constructor
    public Booking(int userId, int serviceId, string jobTitle, 
                   DateTime requestedStart, decimal price)
    {
        UserId = userId;
        ProviderServiceId = serviceId;
        JobTitle = jobTitle;
        RequestedStartAt = requestedStart;
        EstimatedPrice = price;
        BookingCode = GenerateBookingCode();
        Status = BookingStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    // 🎯 Generate Unique Booking Code
    private static string GenerateBookingCode()
    {
        return $"BK{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }
}

// 💡 การใช้งาน
// ✅ ด้วย Parameterized Constructor
var booking = new Booking(
    userId: 1,
    serviceId: 5,
    jobTitle: "ซ่อมเครื่องปรับอ��กาศ",
    requestedStart: DateTime.Now.AddDays(1),
    price: 500.00m
);
// Result: BookingCode = "BK202604091430528245", Status = "Pending", CreatedAt = ✓ set
```

**🎁 ประโยชน์ของการออกแบบนี้**:
- 👉 มั่นใจว่า `BookingCode`, `Status`, `CreatedAt` ถูกตั้งค่าอัตโนมัติ
- 👉 ลดจำนวน Bugs ที่เกิดจากการลืมกำหนดค่า
- 👉 ทำให้ Code สั้นกว่า และเข้าใจง่ายขึ้น

---

## 2. ระบบฐานข้อมูล (Database System)

### 2.1 บทบาทและความสำคัญ

**นิยาม**: ระบบฐานข้อมูล คือวิธีการจัดเก็บข้อมูลอิเล็กทรอนิกส์ที่สามารถประมวลผลเป็นสารสนเทศได้อย่างมีประสิทธิภาพ

**ข้อได้เปรียบ**:
- ✅ ความสามารถในการจัดการข้อมูลขนาดใหญ่
- ✅ รองรับผู้ใช้พร้อมกัน (Concurrency)
- ✅ รักษาความสมบูรณ์ของข้อมูล (Data Integrity)
- ✅ กู้คืนข้อมูลได้อย่างปลอดภัย (Recovery)

**ประโยชน์จากการศึกษา**:
- ลดความซ้ำซ้อนของข้อมูล ได้ถึง **70%** (Codd, 1970)
- เพิ่มประสิทธิภาพการค้นหาข้อมูล **70%** (Date, 2003)

### 2.2 เหตุผลการเลือก SQL Server

| หัวข้อ | ประเด็น | ประโยชน์ |
|-------|--------|--------|
| **🔗 Integration** | ผสานรวม .NET Ecosystem | LINQ to SQL, EF Core เต็มรูปแบบ |
| **🔐 ACID** | Transaction Management | Atomicity, Consistency, Isolation, Durability |
| **⚡ Performance** | Query Optimizer | Indexing หลายรูปแบบ |
| **📈 Scalability** | Enterprise-Grade | รองรับความเติบโต |
| **🛠️ Tools** | SSMS Management | GUI ครบถ้วน |

---

## บทสรุป

การศึกษาเทคโนโลยีและแนวปฏิบัติที่ดีที่สุดในบทนี้ เป็นรากฐานสำคัญในการพัฒนาระบบ BookMyService ที่มีคุณภาพและสามารถตอบสนองความต้องการของผู้ใช้งาน โดยใช้หลักการ OOP ร่วมกับ SQL Server เพื่อสร้างระบบที่ยืดหยุ่น มีประสิทธิภาพสูง และง่ายต่อการบำรุงรักษา

---

**📚 อ้างอิง**:
- Gamma, E., Helm, R., Johnson, R., & Vlissides, J. (1994). Design Patterns: Elements of Reusable Object-Oriented Software.
- Martin, R. C. (2008). Clean Code: A Handbook of Agile Software Craftsmanship.
- Codd, E. F. (1970). A relational model of data for large shared data banks.
- Date, C. J. (2003). An Introduction to Database Systems.
