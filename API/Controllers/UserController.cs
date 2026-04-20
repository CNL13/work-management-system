using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _service;

        public UserController(IUserService service) { _service = service; }

        /// <summary>Lấy danh sách người dùng (Admin xem tất cả, Manager xem phòng mình)</summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetAll()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var idClaim = User.FindFirst("id")?.Value;

            if (role == "Manager" && Guid.TryParse(idClaim, out var managerId))
                return Ok(await _service.GetByManager(managerId));

            return Ok(await _service.GetAll());
        }

        /// <summary>Tìm kiếm nhân viên theo tên/mã NV/role/phòng</summary>
        [HttpGet("search")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Search(
            string? keyword,
            string? role,
            Guid? unitId)
        {
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var idClaim = User.FindFirst("id")?.Value;

            Guid? managerId = null;
            if (userRole == "Manager" && Guid.TryParse(idClaim, out var mid))
                managerId = mid;

            var result = await _service.Search(keyword ?? "", role, unitId, managerId);
            return Ok(result);
        }

        /// <summary>Cập nhật người dùng (chỉ Admin)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, UpdateUserDto dto)
            => Ok(await _service.Update(id, dto));

        /// <summary>Xóa người dùng (chỉ Admin)</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.Delete(id);
            return Ok();
        }

        /// <summary>Xem điểm KPI cá nhân (nhân viên xem của mình, Manager/Admin xem bất kỳ)</summary>
        [HttpGet("performance/{id}")]
        public async Task<IActionResult> GetPerformance(Guid id)
            => Ok(await _service.GetPerformanceAsync(id));

        /// <summary>Xem bảng KPI toàn phòng (Manager xem nhân viên phòng mình)</summary>
        [HttpGet("performance/unit")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetUnitPerformance()
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (!Guid.TryParse(idClaim, out var managerId))
                return Unauthorized();
            return Ok(await _service.GetUnitPerformanceAsync(managerId));
        }
    }
}