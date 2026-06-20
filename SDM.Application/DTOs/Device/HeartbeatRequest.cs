using System;

namespace SDM.Application.DTOs.Device
{
    public class HeartbeatRequest
    {
        public int Battery { get; set; }

        public long FreeStorage { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }
}
