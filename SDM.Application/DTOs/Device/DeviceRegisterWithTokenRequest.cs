using System;

namespace SDM.Application.DTOs.Device
{
    public class DeviceRegisterWithTokenRequest
    {
        public string Token { get; set; } = string.Empty;
        public string DeviceIdentifier { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = string.Empty;
        // Optional FCM token provided at registration so server can notify the device immediately
        public string? FcmToken { get; set; }
    }
}
