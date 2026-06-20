using SDM.Domain.Entities;

namespace SDM.Application.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user);
        string GenerateDeviceToken(User user, int expiryMinutes);
    }
}
