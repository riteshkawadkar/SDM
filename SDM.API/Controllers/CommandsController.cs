using Microsoft.AspNetCore.Mvc;
using SDM.Application.Interfaces;
using System.Text.Json;

namespace SDM.API.Controllers
{
    using Microsoft.AspNetCore.Authorization;

    [ApiController]
    [Route("api/devices/{deviceId:guid}/[controller]")]
    public class CommandsController : ControllerBase
    {
        private readonly ICommandService _commandService;

        public CommandsController(ICommandService commandService)
        {
            _commandService = commandService;
        }

        public class CreateCommandRequest
        {
            public string CommandType { get; set; } = string.Empty;
            // Accepts a JSON string, object, or array — all stored as a JSON string internally.
            public JsonElement Payload { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromRoute] Guid deviceId, [FromBody] CreateCommandRequest request)
        {
            var payloadStr = request.Payload.ValueKind == JsonValueKind.String
                ? request.Payload.GetString() ?? string.Empty
                : request.Payload.ToString();

            var cmd = await _commandService.CreateCommandAsync(deviceId, request.CommandType, payloadStr);
            // Map to DTO to avoid circular JSON references
            var resp = new SDM.Application.DTOs.Command.CommandResponse
            {
                Id = cmd.Id,
                DeviceId = cmd.DeviceId,
                CommandType = cmd.CommandType,
                Payload = cmd.Payload,
                Status = (int)cmd.Status,
                CreatedOn = cmd.CreatedOn,
                ExecutedOn = cmd.ExecutedOn
            };

            return CreatedAtAction(nameof(GetById), new { deviceId = deviceId, id = cmd.Id }, resp);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid deviceId, [FromRoute] Guid id)
        {
            // Query command from DB
            // Simple lookup via context through command service isn't implemented; use DB directly for now
            // To avoid adding new dependencies, return 200 with id
            return Ok(new { Id = id, DeviceId = deviceId });
        }

        [HttpPost("{id:guid}/status")]
        public async Task<IActionResult> ReportStatus([FromRoute] Guid deviceId, [FromRoute] Guid id, [FromBody] ReportStatusRequest request)
        {
            await _commandService.ReportCommandStatusAsync(deviceId, id, request.Success);
            return NoContent();
        }

        public class ReportStatusRequest
        {
            public bool Success { get; set; }
        }
    }
}
