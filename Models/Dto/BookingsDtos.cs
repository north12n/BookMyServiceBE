using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using BookMyService.Models;

namespace BookMyServiceBE.Models.Dto
{
    public record BookingQuery(
        int? CustomerId = null,
        int? ProviderId = null,
        BookingStatus? Status = null,
        DateTime? From = null,
        DateTime? To = null,
        int Page = 1,
        int PageSize = 20)
    {
        public int PageSize { get; init; } = Math.Clamp(PageSize, 1, 100);
        public int Page { get; init; } = Math.Max(Page, 1);
    }

    public class QuickCreateBookingDto
    {
        [Required] public int UserId { get; set; }
        [Required] public int ProviderServiceId { get; set; }
        [Required] public DateTime RequestedStartAt { get; set; }
    }

    public class CreateBookingDto
    {
        [Required] public int UserId { get; set; }
        [Required] public int ProviderServiceId { get; set; }

        [Required, MaxLength(200)] public string JobTitle { get; set; } = null!;
        [MaxLength(1000)] public string? JobDescription { get; set; }

        [Required] public DateTime RequestedStartAt { get; set; }
        public DateTime? RequestedEndAt { get; set; }


        [Required, MaxLength(300)] public string AddressLine { get; set; } = null!;
        [Required, MaxLength(100)] public string District { get; set; } = null!;
        [Required, MaxLength(100)] public string Province { get; set; } = null!;
        [Required, MaxLength(10)] public string PostalCode { get; set; } = null!;

        [Range(0, double.MaxValue)] public decimal EstimatedPrice { get; set; }
    }

    public record CreatedBookingDto(int BookingId, string BookingCode, BookingStatus Status);

    public record ProviderMini(int UserId, string FullName);
    public record CategoryMini(int ServiceCategoryId, string Name);
    public record ServiceMini(int ProviderServiceId, string Title, decimal BasePrice, ProviderMini Provider, CategoryMini Category);

    public record BookingListItemDto(
        int BookingId,
        string BookingCode,
        int CustomerId,
        ServiceMini Service,
        string JobTitle,
        DateTime RequestedStartAt,
        decimal EstimatedPrice,
        decimal? FinalPrice,
        BookingStatus Status,
        DateTime CreatedAt)
    {
        public static Expression<Func<Booking, BookingListItemDto>> Projection =>
            b => new BookingListItemDto(
                b.BookingId,
                b.BookingCode,
                b.UserId,
                new ServiceMini(
                    b.ProviderService.ProviderServiceId,
                    b.ProviderService.Title,
                    b.ProviderService.BasePrice,
                    new ProviderMini(
                        b.ProviderService.Provider.UserId,
                        b.ProviderService.Provider.FullName),
                    new CategoryMini(
                        b.ProviderService.ServiceCategory.ServiceCategoryId,
                        b.ProviderService.ServiceCategory.Name)
                ),
                b.JobTitle,
                b.RequestedStartAt,
                b.EstimatedPrice,
                b.FinalPrice,
                b.Status,
                b.CreatedAt
            );
    }

    public record BookingDetailDto(
        int BookingId,
        string BookingCode,
        int CustomerId,
        ServiceMini Service,
        string JobTitle,
        string? JobDescription,
        string AddressLine,
        string District,
        string Province,
        string PostalCode,
        DateTime RequestedStartAt,
        decimal EstimatedPrice,
        decimal? FinalPrice,
        BookingStatus Status,
        DateTime CreatedAt,
        DateTime? UpdatedAt)
    {
        public static Expression<Func<Booking, BookingDetailDto>> Projection =>
            b => new BookingDetailDto(
                b.BookingId,
                b.BookingCode,
                b.UserId,
                new ServiceMini(
                    b.ProviderService.ProviderServiceId,
                    b.ProviderService.Title,
                    b.ProviderService.BasePrice,
                    new ProviderMini(
                        b.ProviderService.Provider.UserId,
                        b.ProviderService.Provider.FullName),
                    new CategoryMini(
                        b.ProviderService.ServiceCategory.ServiceCategoryId,
                        b.ProviderService.ServiceCategory.Name)
                ),
                b.JobTitle,
                b.JobDescription,
                b.AddressLine,
                b.District,
                b.Province,
                b.PostalCode,
                b.RequestedStartAt,
                b.EstimatedPrice,
                b.FinalPrice,
                b.Status,
                b.CreatedAt,
                b.UpdatedAt
            );
    }

    // วางไว้ที่ Common ที่เดียว ห้ามซ้ำ namespace เดียวกัน
    public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

    public class ChangeBookingStatusDto
    {
        [Required] public BookingStatus NewStatus { get; set; }
        public decimal? FinalPrice { get; set; } // ใช้ตอน Completed
    }
}