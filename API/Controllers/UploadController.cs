using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly IUploadService _uploadService;

        public UploadController(IUploadService uploadService)
        {
            _uploadService = uploadService;
        }

        /// <summary>
        /// Upload file (ảnh/tài liệu)
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, Guid? progressId, Guid? taskId)
        {
            var result = await _uploadService.UploadAsync(file, progressId, taskId);
            return Ok(result);
        }

        /// <summary>
        /// Tải file đính kèm (Cho phép xem trực tiếp qua ID mà không cần token)
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var file = await _uploadService.GetFileByIdAsync(id);
            if (file == null) return NotFound("File not found");

            if (!System.IO.File.Exists(file.FilePath))
                return NotFound("File physical content not found");

            var fileBytes = await System.IO.File.ReadAllBytesAsync(file.FilePath);
            
            // Tự động nhận diện MIME type để trình duyệt có thể hiển thị ảnh/PDF thay vì bắt tải về
            var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
            string contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            return File(fileBytes, contentType, file.FileName);
        }
    }
}