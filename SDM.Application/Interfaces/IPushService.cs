namespace SDM.Application.Interfaces
{
    public interface IPushService
    {
        Task<bool> SendToDeviceAsync(Guid deviceId, string title, string body, object data);
    }
}
