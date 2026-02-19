//namespace BookMyServiceBE.Models.Dto
//{
//    public enum BookingStatus
//    {
//        PendingPayment = 0,   // ลูกค้าจองแล้ว รอชำระ
//        Paid = 1,             // ชำระแล้ว รอจัดคิว/มอบหมาย
//        Assigned = 2,         // มอบหมายช่างแล้ว (optional)
//        InProgress = 3,       // เริ่มงาน / เช็คอินแล้ว
//        Completed = 4,        // จบงาน
//        Cancelled = 5         // ยกเลิก
//    }

//    public static class BookingStatusRules
//    {
//        private static readonly Dictionary<BookingStatus, BookingStatus[]> _flow = new()
//        {
//            [BookingStatus.PendingPayment] = new[] { BookingStatus.Paid, BookingStatus.Cancelled },
//            [BookingStatus.Paid] = new[] { BookingStatus.Assigned, BookingStatus.InProgress, BookingStatus.Cancelled },
//            [BookingStatus.Assigned] = new[] { BookingStatus.InProgress, BookingStatus.Cancelled },
//            [BookingStatus.InProgress] = new[] { BookingStatus.Completed, BookingStatus.Cancelled },
//            [BookingStatus.Completed] = Array.Empty<BookingStatus>(),
//            [BookingStatus.Cancelled] = Array.Empty<BookingStatus>(),
//        };

//        public static bool CanTransit(BookingStatus from, BookingStatus to)
//            => _flow.TryGetValue(from, out var next) && next.Contains(to);
//    }
//}
