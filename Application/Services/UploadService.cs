using Microsoft.AspNetCore.Http;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class UploadService : IUploadService
    {
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _context;

        public UploadService(IWebHostEnvironment env, AppDbContext context)
        {
            _env = env;
            _context = context;
        }

        public async Task<UploadFile> UploadAsync(IFormFile file, Guid? progressId)
        {
            // 1. kiểm tra file
            if (file == null || file.Length == 0)
                throw new Exception("File is empty");

            // 2. tạo folder Uploads
            var folderPath = Path.Combine(_env.ContentRootPath, "Uploads");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // 3. tạo tên file unique
            var newFileName = Guid.NewGuid() + Path.GetExtension(file.FileName);

            var filePath = Path.Combine(folderPath, newFileName);

            // 4. lưu file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 5. lưu DB
            var upload = new UploadFile
            {
                Id = Guid.NewGuid(),
                FileName = file.FileName,
                FilePath = filePath,
                CreatedAt = DateTime.UtcNow,
                ProgressId = progressId
            };

            _context.UploadFiles.Add(upload);
            await _context.SaveChangesAsync();

            return upload;
        }
    }
}