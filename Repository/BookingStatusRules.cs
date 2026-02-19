namespace BookMyService.Models
{
    public static class BookingStatusRules
    {
        public static bool CanTransit(BookingStatus from, BookingStatus to)
        {
            return (from, to) switch
            {
                // Payment flow
                (BookingStatus.PendingPayment, BookingStatus.Paid) => true,

                // Provider flow
                (BookingStatus.Paid, BookingStatus.Assigned) => true,
                (BookingStatus.Assigned, BookingStatus.InProgress) => true,
                (BookingStatus.InProgress, BookingStatus.Completed) => true,

                // Cancel cases
                (BookingStatus.Paid, BookingStatus.CancelledByProvider) => true,
                (BookingStatus.Assigned, BookingStatus.CancelledByProvider) => true,
                (BookingStatus.Paid, BookingStatus.CancelledByCustomer) => true,

                _ => false
            };
        }
    }
}
