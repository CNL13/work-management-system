using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

        public TaskController(ITaskService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy danh sách task (search + filter + pagination)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get(
            string? keyword,
            string? status,
            int page = 1,
            int size = 10)
        {
            var result = await _service.Get(keyword ?? "", page, size, status);
            return Ok(result);
        }

        /// <summary>
        /// Tạo task mới (Manager)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create(CreateTaskDto dto)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);

            var result = await _service.Create(dto, userId);
            return Ok(result);
        }

        /// <summary>
        /// Cập nhật task
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Update(Guid id, CreateTaskDto dto)
        {
            var result = await _service.Update(id, dto);
            return Ok(result);
        }

        /// <summary>
        /// Xóa task
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.Delete(id);
            return Ok(new { message = "Deleted successfully" });
        }
    }
}