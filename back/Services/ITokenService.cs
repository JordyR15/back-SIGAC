using back.Entities;

namespace back.Services
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}
