using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SDM.Infrastructure.Data;
using SDM.Application.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace SDM.Infrastructure.Services
{
    public class HangfireJobs
    {
        private readonly ApplicationDbContext _db;
        private readonly IPushService _pushService;
        private readonly ILogger<HangfireJobs> _logger;

        public HangfireJobs(ApplicationDbContext db, IPushService pushService, ILogger<HangfireJobs> logger)
        {
            _db = db;
            _pushService = pushService;
            _logger = logger;
        }

        // Called by Hangfire recurring job
        public async Task ProcessPendingCommands()
        {
            _logger.LogInformation("Hangfire: Processing pending commands...");

            var pending = await _db.DeviceCommands
                .Where(c => c.Status == SDM.Domain.CommandStatus.Pending || c.Status == SDM.Domain.CommandStatus.Failed)
                .OrderBy(c => c.CreatedOn)
                .Take(50)
                .ToListAsync();

            foreach (var cmd in pending)
            {
                try
                {
                    // respect max retries
                    if (cmd.RetryCount >= cmd.MaxRetries)
                    {
                        cmd.Status = SDM.Domain.CommandStatus.Failed;
                        _logger.LogWarning("Command {CommandId} reached max retries", cmd.Id);
                        continue;
                    }

                    var sent = await _pushService.SendToDeviceAsync(cmd.DeviceId, "command", cmd.CommandType, new { commandId = cmd.Id, payload = cmd.Payload });
                    if (sent)
                    {
                        cmd.Status = SDM.Domain.CommandStatus.Sent;
                        _logger.LogInformation("Command {CommandId} sent to device {DeviceId}", cmd.Id, cmd.DeviceId);
                    }
                    else
                    {
                        cmd.RetryCount++;
                        cmd.Status = SDM.Domain.CommandStatus.Failed;
                        _logger.LogWarning("Failed to send Command {CommandId} to device {DeviceId} (retry {RetryCount}/{MaxRetries})", cmd.Id, cmd.DeviceId, cmd.RetryCount, cmd.MaxRetries);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending command {CommandId}", cmd.Id);
                    cmd.Status = SDM.Domain.CommandStatus.Failed;
                    cmd.RetryCount++;
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}
