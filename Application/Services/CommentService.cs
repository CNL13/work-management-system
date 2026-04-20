using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.Application.Services
{
    public class CommentService : ICommentService
    {
        private readonly IGenericRepository<TaskComment> _repo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<CommentReaction> _reactionRepo;
        private readonly IGenericRepository<CommentSeen> _seenRepo; // ✅ Quản lý trạng thái "Đã xem" bình luận
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        public CommentService(
            IGenericRepository<TaskComment> repo,
            IGenericRepository<User> userRepo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<CommentReaction> reactionRepo,
            IGenericRepository<CommentSeen> seenRepo,
            INotificationService notificationService,
            IMapper mapper)
        {
            _repo = repo;
            _userRepo = userRepo;
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _reactionRepo = reactionRepo;
            _seenRepo = seenRepo;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        /// <summary>
        /// Thêm bình luận mới vào công việc:
        /// 1. Lưu nội dung bình luận.
        /// 2. Tự động đánh dấu chính người gửi là đã xem bình luận này.
        /// 3. Gửi thông báo đến người tạo công việc và những người được giao (trừ người gửi).
        /// </summary>
        public async Task<CommentDto> AddComment(CreateCommentDto dto, Guid userId)
        {
            var comment = new TaskComment
            {
                Id = Guid.NewGuid(),
                TaskId = dto.TaskId,
                UserId = userId,
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(comment);
            await _repo.SaveAsync();

            // Tự động đánh dấu người gửi đã xem tin nhắn của chính mình
            await _seenRepo.AddAsync(new CommentSeen { Id = Guid.NewGuid(), CommentId = comment.Id, UserId = userId });
            await _seenRepo.SaveAsync();

            // Logic gửi thông báo
            var task = await _taskRepo.GetByIdAsync(dto.TaskId);
            var sender = await _userRepo.GetByIdAsync(userId);

            // Lấy danh sách ID người được giao (Assignees)
            var assignees = await _assigneeRepo.Query()
                .Where(a => a.TaskId == dto.TaskId && a.UserId.HasValue && a.UserId.Value != userId)
                .Select(a => a.UserId.Value)
                .ToListAsync();

            // Thêm người tạo công việc vào danh sách nhận thông báo nếu họ không phải người gửi
            if (task != null && task.CreatedBy != userId)
            {
                assignees.Add(task.CreatedBy);
            }

            // Gửi thông báo không trùng lặp
            var uniqueRecipients = assignees.Distinct();
            foreach (var recipientId in uniqueRecipients)
            {
                await _notificationService.AddNotification(recipientId,
                    $"{sender?.FullName} đã bình luận trong công việc: {task?.Title}");
            }

            var result = _mapper.Map<CommentDto>(comment);
            result.UserFullName = sender?.FullName;
            result.UserEmployeeCode = sender?.EmployeeCode;
            return result;
        }

        /// <summary>
        /// Lấy danh sách bình luận của một Task:
        /// 1. Truy vấn toàn bộ comment, cảm xúc (Reactions) và danh sách người đã xem (Seens).
        /// 2. Gom nhóm cảm xúc theo từng loại Emoji kèm danh sách tên người dùng.
        /// 3. Trả về thông tin người dùng (Tên, Mã nhân viên) cho mỗi bình luận.
        /// </summary>
        public async Task<List<CommentDto>> GetComments(Guid taskId, Guid? userId = null)
        {
            var comments = await _repo.Query()
                .Where(c => c.TaskId == taskId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            var commentIds = comments.Select(c => c.Id).ToList();

            // Lấy dữ liệu Reactions và Seens liên quan đến danh sách comment
            var allReactions = await _reactionRepo.Query()
                .Where(r => commentIds.Contains(r.CommentId))
                .ToListAsync();

            var allSeens = await _seenRepo.Query()
                .Where(s => commentIds.Contains(s.CommentId))
                .ToListAsync();

            // Tập hợp ID người dùng để lấy thông tin FullName/EmployeeCode (tối ưu truy vấn)
            var userIds = comments.Select(c => c.UserId)
                .Concat(allReactions.Select(r => r.UserId))
                .Concat(allSeens.Select(s => s.UserId))
                .Distinct().ToList();

            var users = await _userRepo.Query()
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            var dtos = comments.Select(c =>
            {
                var dto = _mapper.Map<CommentDto>(c);
                var user = users.FirstOrDefault(u => u.Id == c.UserId);
                dto.UserFullName = user?.FullName;
                dto.UserEmployeeCode = user?.EmployeeCode;

                // Xử lý thống kê cảm xúc (Emoji, Số lượng, Danh sách người thả)
                var reactions = allReactions.Where(r => r.CommentId == c.Id).ToList();
                dto.Reactions = reactions
                    .GroupBy(r => r.Emoji)
                    .Select(g => new ReactionSummaryDto
                    {
                        Emoji = g.Key,
                        Count = g.Count(),
                        UserFullNames = users
                            .Where(u => g.Select(x => x.UserId).Contains(u.Id))
                            .Select(u => u.FullName)
                            .ToList()
                    })
                    .ToList();

                // ✅ Xử lý danh sách người đã xem bình luận
                dto.SeenByUserFullNames = allSeens
                    .Where(s => s.CommentId == c.Id)
                    .Select(s => users.FirstOrDefault(u => u.Id == s.UserId)?.FullName ?? "Unknown")
                    .Distinct()
                    .ToList();

                // Xác định cảm xúc hiện tại của người đang truy cập (nếu có)
                if (userId.HasValue)
                {
                    dto.MyReaction = reactions.FirstOrDefault(r => r.UserId == userId.Value)?.Emoji;
                }

                return dto;
            }).ToList();

            return dtos;
        }

        /// <summary>
        /// Đánh dấu toàn bộ bình luận trong một Task là "Đã xem":
        /// Kiểm tra những bình luận nào user chưa xem thì sẽ thêm bản ghi vào bảng CommentSeen.
        /// </summary>
        public async Task MarkAsSeen(Guid taskId, Guid userId)
        {
            var comments = await _repo.Query()
                .Where(c => c.TaskId == taskId)
                .ToListAsync();

            var commentIds = comments.Select(c => c.Id).ToList();

            // Tìm các comment đã được user xem trước đó
            var existingSeens = await _seenRepo.Query()
                .Where(s => s.UserId == userId && commentIds.Contains(s.CommentId))
                .Select(s => s.CommentId)
                .ToListAsync();

            // Lọc ra các comment thực sự chưa xem
            var unseenCommentIds = commentIds.Except(existingSeens).ToList();
            if (unseenCommentIds.Any())
            {
                foreach (var cid in unseenCommentIds)
                {
                    await _seenRepo.AddAsync(new CommentSeen
                    {
                        Id = Guid.NewGuid(),
                        CommentId = cid,
                        UserId = userId,
                        SeenAt = DateTime.UtcNow
                    });
                }
                await _seenRepo.SaveAsync();
            }
        }

        /// <summary>
        /// Bật/Tắt cảm xúc (Reaction) cho bình luận:
        /// 1. Nếu chưa có cảm xúc -> Thêm mới.
        /// 2. Nếu đã có cảm xúc và trùng Emoji -> Xóa (Bỏ chọn).
        /// 3. Nếu đã có cảm xúc nhưng khác Emoji -> Cập nhật lại Emoji mới.
        /// </summary>
        public async Task<Guid> ToggleReaction(Guid commentId, Guid userId, string emoji)
        {
            var comment = await _repo.GetByIdAsync(commentId)
                ?? throw new Exception("Comment not found");

            var existing = await _reactionRepo.Query()
                .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId);

            if (existing != null)
            {
                if (existing.Emoji == emoji)
                {
                    _reactionRepo.Delete(existing); // Bỏ chọn cảm xúc
                }
                else
                {
                    existing.Emoji = emoji; // Đổi cảm xúc (Ví dụ từ Like sang Love)
                    _reactionRepo.Update(existing);
                }
            }
            else
            {
                await _reactionRepo.AddAsync(new CommentReaction
                {
                    Id = Guid.NewGuid(),
                    CommentId = commentId,
                    UserId = userId,
                    Emoji = emoji
                });
            }

            await _reactionRepo.SaveAsync();
            return comment.TaskId;
        }

        /// <summary>
        /// Xóa bình luận:
        /// 1. Kiểm tra quyền (Chỉ người viết bình luận mới được xóa).
        /// 2. Thực hiện xóa mềm (IsDeleted = true).
        /// </summary>
        public async Task Delete(Guid commentId, Guid userId)
        {
            var comment = await _repo.GetByIdAsync(commentId)
                ?? throw new Exception("Comment not found");

            if (comment.UserId != userId)
                throw new Exception("Bạn không có quyền xóa bình luận của người khác!");

            comment.IsDeleted = true;
            _repo.Update(comment);
            await _repo.SaveAsync();
        }
    }
}