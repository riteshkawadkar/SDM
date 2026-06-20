using System;

namespace SDM.Application.DTOs.Enrollment
{
    public class CreateEnrollmentRequest
    {
        public int MaxDevices { get; set; } = 1;
        public int ExpiresInMinutes { get; set; } = 60;
    }
}
