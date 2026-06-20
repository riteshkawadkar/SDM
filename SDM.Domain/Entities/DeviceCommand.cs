using System;

namespace SDM.Domain.Entities
{
    public class DeviceCommand
    {
        public Guid Id { get; set; }

        public Guid DeviceId { get; set; }

        public string CommandType { get; set; } = string.Empty;

        // JSON payload for the command
        public string Payload { get; set; } = string.Empty;

        public CommandStatus Status { get; set; } = CommandStatus.Pending;

        // Retry tracking for push delivery
        public int RetryCount { get; set; } = 0;

        public int MaxRetries { get; set; } = 5;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public DateTime? ExecutedOn { get; set; }

        public Device? Device { get; set; }
    }
}
