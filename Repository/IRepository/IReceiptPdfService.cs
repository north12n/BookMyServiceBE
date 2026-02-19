using BookMyService.Models;

namespace BookMyServiceBE.Repository.IRepository
{
    public interface IReceiptPdfService
    {
        byte[] CreateBookingReceiptPdf(Booking booking);
    }
}
