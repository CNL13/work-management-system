using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/progress")]
    public class ProgressController : ControllerBase
    {
        private readonly IProgressService _service;
        private readonly IGenericRepository<User> _userRepo;  // ✅ MỚI

        public ProgressController(
            IProgressService service,
            IGenericRepository<User> userRepo)  // ✅ MỚI
        {
            _service = service;
            _userRepo = userRepo;  // ✅ MỚI
        }

        /// <summary>
        /// Xem danh sách tiến độ (có phân trang)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            int page = 1,
            int size = 10,
            bool myProgress = false)
        {
            var role = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value)?.Trim();
            var idClaim = (User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            Guid? userId = null;
            Guid? unitId = null;

            if (myProgress && Guid.TryParse(idClaim, out var parsedId))
                userId = parsedId;

            // ✅ SỬA: Manager chỉ xem progress của phòng mình (case-insensitive)
            if (role != null && role.Equals("Manager", StringComparison.OrdinalIgnoreCase) && !myProgress && Guid.TryParse(idClaim, out var mid))
            {
                var manager = await _userRepo.GetByIdAsync(mid);
                unitId = manager?.UnitId;
            }

            return Ok(await _service.GetAll(page, size, userId, unitId));
        }

        /// <summary>
        /// Cập nhật tiến độ công việc
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Update(CreateProgressDto dto)
        {
            var idClaim = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idClaim, out var userId))
            {
                dto.UserId = userId; // ✅ FIX BUG: Override UserId bằng ID thật từ server token
            }
            return Ok(await _service.Update(dto));
        }

        /// <summary>
        /// Lấy lịch sử báo cáo tiến độ của 1 Task cụ thể
        /// </summary>
        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTask(Guid taskId)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            return Ok(await _service.GetByTaskAsync(taskId, userId));
        }

        /// <summary>
        /// Lịch sử cá nhân: Xem TẤT CẢ báo cáo từ mọi phòng ban (không lọc Unit)
        /// </summary>
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory(int page = 1, int size = 20)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            return Ok(await _service.GetMyHistory(userId, page, size));
        }
    }
}
