using System;

namespace SDM.Domain.Entities
{
    public class DevicePushToken
    {
        public Guid Id { get; set; }

        public Guid DeviceId { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public Device? Device { get; set; }
    }
}
