using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/tasks")]
    public class TaskController : ControllerBase
    {
        private readonly ITaskService _service;
        public TaskController(ITaskService service) { _service = service; }

        /// <summary>Lấy danh sách task (search + filter + pagination)</summary>
        [HttpGet]
        public async Task<IActionResult> Get(
            string? keyword,
            string? status,
            int page = 1,
            int size = 10,
            bool myTasks = false)
        {
            var role = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value)?.Trim()?.ToLower();
            var idClaim = (User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            Guid? userId = null;

            if (myTasks && Guid.TryParse(idClaim, out var parsedId))
                userId = parsedId;

            Guid? managerUnitId = null;
            if (role == "manager" && Guid.TryParse(idClaim, out var mid))
                managerUnitId = await _service.GetManagerUnitId(mid);

            Guid currentUserId = Guid.Parse(idClaim!);
            var result = await _service.Get(keyword ?? "", page, size, status, currentUserId, userId, managerUnitId);
            return Ok(result);
        }

        /// <summary>Tạo task mới (Manager — chỉ cho phòng mình)</summary>
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create(CreateTaskDto dto)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var result = await _service.Create(dto, userId);
            return Ok(result);
        }

        /// <summary>Cập nhật task (Manager) — có ghi lịch sử thay đổi</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> Update(Guid id, CreateTaskDto dto)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();
            return Ok(await _service.Update(id, dto, userId));
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] string status)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

            await _service.UpdateStatus(id, status, userId);
            return Ok();
        }

        /// <summary>Xóa task (Bảo mật: Chỉ Creator hoặc Manager mới được xóa)</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            await _service.Delete(id, userId);
            return Ok(new { message = "Deleted successfully" });
        }

        /// <summary>Cập nhật vị trí thẻ Kanban</summary>
        [HttpPut("{id}/reorder")]
        public async Task<IActionResult> Reorder(Guid id, [FromBody] int newIndex)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            await _service.Reorder(id, newIndex, userId);
            return Ok();
        }

        /// <summary>Đôn đốc deadline (Manager)</summary>
        [HttpPost("{id}/remind")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Remind(Guid id)
        {
            var managerId = Guid.Parse(User.FindFirst("id")!.Value);
            await _service.RemindTask(id, managerId);
            return Ok(new { message = "Đã gửi nhắc nhở đôn đốc thành công!" });
        }

        /// <summary>Lấy chi tiết công việc</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var result = await _service.GetByIdAsync(id, userId);
            return Ok(result);
        }

        /// <summary>Lấy lịch sử thay đổi của công việc</summary>
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var result = await _service.GetHistoryAsync(id, userId);
            return Ok(result);
        }
    }
}
