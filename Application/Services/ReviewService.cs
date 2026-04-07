using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _context;

        public ReviewService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ReviewDto> Review(ReviewDto dto)  // Task → Task<ReviewDto>
        {
            var progress = await _context.Progresses.FindAsync(dto.ProgressId);
            if (progress == null) throw new Exception("Progress not found");
            progress.Status = dto.Approve ? TaskStatus.Approved : TaskStatus.Rejected;
            var review = new ReportReview
            {
                Id = Guid.NewGuid(),
                ProgressId = dto.ProgressId,
                IsApproved = dto.Approve,
                Comment = dto.Comment,
                ReviewedAt = DateTime.UtcNow
            };
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            return dto;  // ✅ thêm dòng này
        }
    }
}