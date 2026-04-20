using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/subtasks")]
    public class SubTaskController : ControllerBase
    {
        private readonly ISubTaskService _service;

        public SubTaskController(ISubTaskService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateSubTaskDto dto)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var result = await _service.AddSubTask(dto, userId);
            return Ok(result);
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> Toggle(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            await _service.ToggleSubTask(id, userId);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            await _service.Delete(id, userId);
            return Ok();
        }

        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTask(Guid taskId)
        {
            var result = await _service.GetSubTasks(taskId);
            return Ok(result);
        }
    }
}
