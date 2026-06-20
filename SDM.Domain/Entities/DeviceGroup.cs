using System;

namespace SDM.Domain.Entities
{
    public class DeviceGroup
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}
