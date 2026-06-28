using SDM.Application.DTOs.Device;
using SDM.Domain.Entities;

namespace SDM.Application.Interfaces
{
    public interface IDeviceService
    {
        Task<Device> RegisterAsync(DeviceRegisterRequest request);

        Task UpdateHeartbeatAsync(Guid deviceId, HeartbeatRequest request);

        Task RegisterPushTokenAsync(Guid deviceId, string token);

        Task<IEnumerable<Device>> GetAllAsync();

        Task<DeviceRegisterWithTokenResponse> RegisterWithTokenAsync(DeviceRegisterWithTokenRequest request);

        Task DeleteAsync(Guid deviceId);
    }
}
