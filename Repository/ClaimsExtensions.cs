using System.Security.Claims;

namespace BookMyServiceBE.Repository
{
    public static class ClaimsExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(sub, out var id) ? id : 0;

            
        }

        public static string? GetRole(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.Role);

        public static string? GetEmail(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
    }
}
