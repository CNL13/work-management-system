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
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, Guid? progressId)
        {
            var result = await _uploadService.UploadAsync(file, progressId);
            return Ok(result);
        }
    }
}