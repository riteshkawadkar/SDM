using System;

namespace SDM.Domain.Entities
{
    public class DeviceHeartbeat
    {
        public Guid Id { get; set; }

        public Guid DeviceId { get; set; }

        public int BatteryLevel { get; set; }

        public long FreeStorage { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public Device? Device { get; set; }
    }
}
