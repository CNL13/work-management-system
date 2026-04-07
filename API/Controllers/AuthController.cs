using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _service;

        public AuthController(IAuthService service)
        {
            _service = service;
        }

        /// <summary>
        /// Đăng ký tài khoản
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register(AuthDto dto)
            => Ok(await _service.Register(dto));

        /// <summary>
        /// Đăng nhập và lấy JWT token
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
            => Ok(await _service.Login(dto.Username, dto.Password));
    }
}