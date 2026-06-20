using System;

namespace SDM.Domain.Entities
{
    public class Policy
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Store JSON policy as text/json
        public string PolicyJson { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
