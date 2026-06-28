using Microsoft.AspNetCore.Mvc;
using SDM.Application.DTOs.Device;
using SDM.Application.Interfaces;

namespace SDM.API.Controllers
{
    using Microsoft.AspNetCore.Authorization;

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _deviceService;

        public DevicesController(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] DeviceRegisterRequest request)
        {
            var device = await _deviceService.RegisterAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
        }

        [AllowAnonymous]
        [HttpPost("register-with-token")]
        public async Task<IActionResult> RegisterWithToken([FromBody] SDM.Application.DTOs.Device.DeviceRegisterWithTokenRequest request)
        {
            var resp = await _deviceService.RegisterWithTokenAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = resp.DeviceId }, resp);
        }

        [HttpPost("{id:guid}/heartbeat")]
        public async Task<IActionResult> Heartbeat([FromRoute] Guid id, [FromBody] HeartbeatRequest request)
        {
            await _deviceService.UpdateHeartbeatAsync(id, request);
            return NoContent();
        }

        [HttpPost("{id:guid}/push-token")]
        public async Task<IActionResult> RegisterPushToken([FromRoute] Guid id, [FromBody] PushTokenRequest request)
        {
            await _deviceService.RegisterPushTokenAsync(id, request.Token);
            return NoContent();
        }

        [HttpPost("update-fcm-token")]
        public async Task<IActionResult> UpdateFcmToken([FromBody] FcmUpdateRequest request)
        {
            // Identify device from bearer token
            var sub = User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var deviceId))
            {
                return Unauthorized();
            }

            await _deviceService.RegisterPushTokenAsync(deviceId, request.FcmToken);
            return NoContent();
        }

        public class FcmUpdateRequest
        {
            public string FcmToken { get; set; } = string.Empty;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var devices = await _deviceService.GetAllAsync();
            return Ok(devices);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var devices = await _deviceService.GetAllAsync();
            var device = devices.FirstOrDefault(d => d.Id == id);
            if (device == null) return NotFound();
            return Ok(device);
        }

        [AllowAnonymous]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            try
            {
                await _deviceService.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex) when (ex.Message == "Device not found")
            {
                return NotFound();
            }
        }
    }
}
