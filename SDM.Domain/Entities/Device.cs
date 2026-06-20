using System;

namespace SDM.Domain.Entities
{
    public class Device
    {
        public Guid Id { get; set; }

        public string DeviceIdentifier { get; set; } = string.Empty; // e.g., Android device id

        public string SerialNumber { get; set; } = string.Empty;

        public string Manufacturer { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string AndroidVersion { get; set; } = string.Empty;

        public int BatteryLevel { get; set; }

        public DateTime? LastSeen { get; set; }

        public DeviceStatus Status { get; set; } = DeviceStatus.Offline;

        public Guid? GroupId { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedOn { get; set; }

        public ICollection<DeviceCommand> Commands { get; set; } = new List<DeviceCommand>();

        public ICollection<DeviceHeartbeat> Heartbeats { get; set; } = new List<DeviceHeartbeat>();

        public ICollection<DevicePushToken> PushTokens { get; set; } = new List<DevicePushToken>();
    }
}
