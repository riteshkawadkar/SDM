using Microsoft.EntityFrameworkCore;
using SDM.Application.Interfaces;
using SDM.Domain.Entities;
using SDM.Domain;
using SDM.Infrastructure.Data;

namespace SDM.Infrastructure.Services
{
    public class CommandService : ICommandService
    {
        private readonly ApplicationDbContext _db;
        private readonly IPushService _pushService;

        public CommandService(ApplicationDbContext db, IPushService pushService)
        {
            _db = db;
            _pushService = pushService;
        }

        public async Task<DeviceCommand> CreateCommandAsync(Guid deviceId, string commandType, string payload)
        {
            var device = await _db.Devices.FindAsync(deviceId);
            if (device == null) throw new Exception("Device not found");

            var cmd = new DeviceCommand
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                CommandType = commandType,
                Payload = payload,
                Status = CommandStatus.Pending,
                CreatedOn = DateTime.UtcNow
            };

            _db.DeviceCommands.Add(cmd);
            await _db.SaveChangesAsync();

            // Try to send push notification
            try
            {
                var sent = await _pushService.SendToDeviceAsync(deviceId, "command", commandType, new { commandId = cmd.Id, payload });
                cmd.Status = sent ? CommandStatus.Sent : CommandStatus.Failed;
                if (sent)
                {
                    // leave ExecutedOn null until device reports
                }
                else
                {
                    // increment retry count
                    cmd.RetryCount++;
                }
            }
            catch
            {
                cmd.Status = CommandStatus.Failed;
                cmd.RetryCount++;
            }

            await _db.SaveChangesAsync();

            // Audit
            _db.AuditLogs.Add(new SDM.Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "CommandCreated",
                EntityName = "DeviceCommand",
                EntityId = cmd.Id,
                NewValue = System.Text.Json.JsonSerializer.Serialize(new { cmd.Id, cmd.CommandType, cmd.Payload }),
                Timestamp = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            return cmd;
        }

        public async Task ReportCommandStatusAsync(Guid deviceId, Guid commandId, bool success)
        {
            var cmd = await _db.DeviceCommands.FirstOrDefaultAsync(c => c.Id == commandId && c.DeviceId == deviceId);
            if (cmd == null) throw new Exception("Command not found");

            cmd.Status = success ? CommandStatus.Executed : CommandStatus.Failed;
            cmd.ExecutedOn = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
    }
}
