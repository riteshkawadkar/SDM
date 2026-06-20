using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SDM.Application.DTOs.Auth;
using SDM.Application.Interfaces;

namespace SDM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                return Ok(await _authService.LoginAsync(request));
            }
            catch (SDM.Application.Exceptions.AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            await _authService.RegisterAsync(request);

            return Ok();
        }
    }
}
