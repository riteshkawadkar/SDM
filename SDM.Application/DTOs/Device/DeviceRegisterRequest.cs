using System;

namespace SDM.Application.DTOs.Device
{
    public class DeviceRegisterRequest
    {
        public string DeviceIdentifier { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public string Manufacturer { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string AndroidVersion { get; set; } = string.Empty;
    }
}
