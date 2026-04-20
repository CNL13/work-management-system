using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IGenericRepository<Progress> _progressRepo;
        private readonly IGenericRepository<ReportReview> _reviewRepo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly INotificationService _notificationService;

        public ReviewService(
            IGenericRepository<Progress> progressRepo,
            IGenericRepository<ReportReview> reviewRepo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            INotificationService notificationService)
        {
            _progressRepo = progressRepo;
            _reviewRepo = reviewRepo;
            _taskRepo = taskRepo;
            _userRepo = userRepo;
            _assigneeRepo = assigneeRepo;
            _notificationService = notificationService;
        }

        public async Task<ReviewDto> Review(ReviewDto dto, Guid reviewerId)
        {
            var progress = await _progressRepo.GetByIdAsync(dto.ProgressId)
                ?? throw new Exception("Progress not found");

            var reviewer = await _userRepo.GetByIdAsync(reviewerId);
            if (reviewer != null && reviewer.Role == "Manager")
            {
                var submitter = await _userRepo.GetByIdAsync(progress.UserId);
                if (submitter != null && reviewer.UnitId != submitter.UnitId)
                    throw new Exception("Bạn không có quyền duyệt báo cáo của phòng khác!");
            }

            var task = await _taskRepo.GetByIdAsync(progress.TaskId)
                ?? throw new Exception("Task not found");

            progress.Status = dto.Approve ? TaskStatus.Approved : TaskStatus.Rejected;
            _progressRepo.Update(progress);

            // ✅ LƯU TRẠNG THÁI BÁO CÁO TRƯỚC KHI TÍNH TOÁN TIẾN ĐỘ TỔNG
            await _progressRepo.SaveAsync(); 

            await _reviewRepo.AddAsync(new ReportReview
            {
                Id = Guid.NewGuid(),
                ProgressId = dto.ProgressId,
                IsApproved = dto.Approve,
                Comment = dto.Comment,
                ReviewedAt = DateTime.UtcNow,
                ReviewerId = reviewerId // ✅ MỚI
            });

            // ✅ MỚI: Trừ ActualHours nếu bị từ chối
            if (!dto.Approve)
            {
                task.ActualHours -= progress.HoursSpent;
                if (task.ActualHours < 0) task.ActualHours = 0;
            }

            if (dto.Approve && progress.Percent >= 100)
            {
                // 1. Lấy danh sách các UserIds được giao Task này
                var assigneeIds = await _assigneeRepo.Query()
                    .Where(a => a.TaskId == task.Id && a.UserId.HasValue)
                    .Select(a => a.UserId.Value)
                    .ToListAsync();

                // 2. Lấy danh sách những người đã có Báo cáo được DUYỆT (Approved) 100%
                var approvedCount = await _progressRepo.Query()
                    .Where(p => p.TaskId == task.Id && p.Status == TaskStatus.Approved && p.Percent >= 100)
                    .Select(p => p.UserId)
                    .Distinct()
                    .CountAsync();

                if (approvedCount >= assigneeIds.Count && assigneeIds.Count > 0)
                {
                    task.Status = TaskStatus.Approved;
                }
                else if (task.Status != TaskStatus.Approved)
                {
                    task.Status = TaskStatus.InProgress; // ✅ FIX BUG: Khong ha cap task da Approved
                }
            }
            else if (task.Status != TaskStatus.Approved)
            {
                task.Status = TaskStatus.InProgress; // ✅ FIX BUG: Khong ha cap task da Approved
            }

            _taskRepo.Update(task);
            await _taskRepo.SaveAsync(); // Đảm bảo lưu task
            await _reviewRepo.SaveAsync(); // Lưu bản ghi review

            var message = dto.Approve
                ? $"✅ Báo cáo của bạn đã được phê duyệt!{(string.IsNullOrEmpty(dto.Comment) ? "" : $" Ghi chú: {dto.Comment}")}"
                : $"❌ Báo cáo của bạn bị từ chối!{(string.IsNullOrEmpty(dto.Comment) ? "" : $" Lý do: {dto.Comment}")}";

            await _notificationService.AddNotification(progress.UserId, message);

            return dto;
        }
    }
}
