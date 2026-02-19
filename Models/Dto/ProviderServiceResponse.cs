using System.Linq.Expressions;
using BookMyService.Models;

namespace BookMyServiceBE.Models.Dto
{
    public record ProviderInfoDto(int UserId, string FullName);
    public record CategoryInfoDto(int ServiceCategoryId, string Name);

    public record ProviderServiceResponse(
        int ProviderServiceId,
        ProviderInfoDto Provider,
        CategoryInfoDto Category,
        string Title,
        string? Description,
        decimal BasePrice,
        string? UnitLabel,
        bool IsActive,
        DateTime CreatedAt,
        List<string> Images
    );

    public static class ProviderServiceResponseMap
    {
        public static Expression<Func<ProviderService, ProviderServiceResponse>> Projection =>
            s => new ProviderServiceResponse(
                s.ProviderServiceId,
                new ProviderInfoDto(s.Provider.UserId, s.Provider.FullName),
                new CategoryInfoDto(s.ServiceCategory.ServiceCategoryId, s.ServiceCategory.Name),
                s.Title,
                s.Description,
                s.BasePrice,
                s.UnitLabel,
                s.IsActive,
                s.CreatedAt,
                s.Images ?? new List<string>()
            );
    }
}