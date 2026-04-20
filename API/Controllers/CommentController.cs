using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using WorkManagementSystem.API.Hubs;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/comments")]
    public class CommentController : ControllerBase
    {
        private readonly ICommentService _service;
        private readonly IHubContext<DiscussionHub> _hubContext; // ✅ MỚI

        public CommentController(ICommentService service, IHubContext<DiscussionHub> hubContext)
        {
            _service = service;
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> Add(CreateCommentDto dto)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

            var result = await _service.AddComment(dto, userId);
            
            // ✅ MỚI: Broadcast tới group của công việc
            await _hubContext.Clients.Group(dto.TaskId.ToString()).SendAsync("ReceiveComment", result);

            return Ok(result);
        }

        [HttpGet("{taskId}")]
        public async Task<IActionResult> GetByTaskId(Guid taskId)
        {
            var idClaim = User.FindFirst("id")?.Value;
            Guid? userId = Guid.TryParse(idClaim, out var id) ? id : null;
            return Ok(await _service.GetComments(taskId, userId));
        }

        [HttpPost("{id}/react")]
        public async Task<IActionResult> React(Guid id, [FromBody] string emoji)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

            var taskId = await _service.ToggleReaction(id, userId, emoji);

            // ✅ MỚI: Broadcast cập nhật cảm xúc
            await _hubContext.Clients.Group(taskId.ToString()).SendAsync("UpdateReaction", id);

            return Ok();
        }

        [HttpPost("task/{taskId}/seen")]
        public async Task<IActionResult> MarkSeen(Guid taskId)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

            await _service.MarkAsSeen(taskId, userId);
            
            // ✅ MỚI: Broadcast rằng có người đã xem toàn bộ tin nhắn trong task này
            await _hubContext.Clients.Group(taskId.ToString()).SendAsync("UpdateSeen", userId);

            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var idClaim = User.FindFirst("id")?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

            await _service.Delete(id, userId);
            return Ok();
        }
    }
}
