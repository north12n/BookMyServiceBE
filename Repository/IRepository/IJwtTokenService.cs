using BookMyService.Models;

namespace BookMyServiceBE.Repository.IRepository
{
    public interface IJwtTokenService
    {
        string CreateToken(User user);
    }
}

