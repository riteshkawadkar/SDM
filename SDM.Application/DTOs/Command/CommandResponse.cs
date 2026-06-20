using System;

namespace SDM.Application.DTOs.Command
{
    public class CommandResponse
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public string CommandType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public int Status { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ExecutedOn { get; set; }
    }
}
