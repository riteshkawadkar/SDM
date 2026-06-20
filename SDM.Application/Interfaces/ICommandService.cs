using SDM.Domain.Entities;

namespace SDM.Application.Interfaces
{
    public interface ICommandService
    {
        Task<DeviceCommand> CreateCommandAsync(Guid deviceId, string commandType, string payload);
        Task ReportCommandStatusAsync(Guid deviceId, Guid commandId, bool success);
    }
}
