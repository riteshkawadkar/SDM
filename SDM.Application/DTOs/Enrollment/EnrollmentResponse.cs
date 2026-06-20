using System;

namespace SDM.Application.DTOs.Enrollment
{
    public class EnrollmentResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresOn { get; set; }
        public int MaxDevices { get; set; }
    }
}
