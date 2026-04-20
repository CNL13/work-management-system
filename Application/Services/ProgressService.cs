using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class ProgressService : IProgressService
    {
        private readonly IGenericRepository<Progress> _repo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<UserUnit> _userUnitRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<UploadFile> _uploadRepo;
        private readonly IGenericRepository<ReportReview> _reviewRepo;
        private readonly IGenericRepository<Unit> _unitRepo; // ✅ MỚI
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        public ProgressService(
            IGenericRepository<Progress> repo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<UserUnit> userUnitRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<UploadFile> uploadRepo,
            IGenericRepository<ReportReview> reviewRepo,
            IGenericRepository<Unit> unitRepo, // ✅ MỚI
            INotificationService notificationService,
            IMapper mapper)
        {
            _repo = repo;
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _userUnitRepo = userUnitRepo;
            _userRepo = userRepo;
            _uploadRepo = uploadRepo;
            _reviewRepo = reviewRepo;
            _unitRepo = unitRepo; // ✅ MỚI
            _notificationService = notificationService;
            _mapper = mapper;
        }

        public async Task<ProgressDto> Update(CreateProgressDto dto)
        {
            // ✅ MỚI: Check quyền (Chỉ người được giao mới được báo cáo)
            bool isAssigned = await _assigneeRepo.Query().AnyAsync(a => a.TaskId == dto.TaskId && a.UserId == dto.UserId);
            if (!isAssigned) throw new UnauthorizedAccessException("Bạn không được phân công công việc này, không thể báo cáo tiến độ.");
            if (!dto.FileId.HasValue) throw new Exception("Vui lòng đính kèm file minh chứng cho báo cáo của bạn.");

            // ✅ Ràng buộc Valid %
            if (dto.Percent < 0) dto.Percent = 0;
            if (dto.Percent > 100) dto.Percent = 100;

            // ✅ MỚI: Chặn báo cáo 100% lặp lại nếu có báo cáo đang chờ hoặc đã duyệt
            if (dto.Percent == 100)
            {
                bool hasCompleted = await _repo.Query().AnyAsync(p => p.TaskId == dto.TaskId && p.UserId == dto.UserId && p.Percent == 100 && (p.Status == TaskStatus.Submitted || p.Status == TaskStatus.Approved));
                if (hasCompleted) throw new Exception("Bạn đã nộp báo cáo 100% hiện đang chờ duyệt hoặc đã được duyệt. Không thể nộp thêm.");
            }

            var progress = _mapper.Map<Progress>(dto);
            progress.Id = Guid.NewGuid();
            progress.Status = dto.Percent == 100 ? TaskStatus.Submitted : TaskStatus.InProgress;
            progress.UpdatedAt = DateTime.UtcNow;
            progress.HoursSpent = dto.HoursSpent; // ✅ MỚI
            await _repo.AddAsync(progress);

            // Cập nhật status và ActualHours của Task tương ứng (Fix lỗi State Machine)
            var task = await _taskRepo.GetByIdAsync(dto.TaskId);
            if (task != null)
            {
                task.Status = dto.Percent == 100 ? TaskStatus.Submitted : TaskStatus.InProgress;
                task.ActualHours += dto.HoursSpent; // ✅ MỚI: Cộng dồn thời gian thực tế
                _taskRepo.Update(task);
            }

            // Liên kết File đính kèm vào Progress nếu có
            if (dto.FileId.HasValue)
            {
                var file = await _uploadRepo.GetByIdAsync(dto.FileId.Value);
                if (file != null)
                {
                    file.ProgressId = progress.Id;
                    _uploadRepo.Update(file);
                }
            }

            await _repo.SaveAsync();

            // Gửi thông báo cho Manager của phòng
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user?.UnitId != null)
            {
                var managers = await _userRepo.Query()
                    .Where(u => u.Role == "Manager" && u.UnitId == user.UnitId)
                    .ToListAsync();
                foreach (var mgr in managers)
                {
                    await _notificationService.AddNotification(mgr.Id,
                        $"Nhân viên {user.FullName} đã gửi báo cáo tiến độ cho công việc: {task?.Title ?? ""}");
                }
            }

            return _mapper.Map<ProgressDto>(progress);
        }

        public async Task<object> GetAll(int page, int size, Guid? userId = null, Guid? unitId = null)
        {
            var query = _repo.Query();

            if (userId.HasValue)
                query = query.Where(p => p.UserId == userId.Value);

            if (unitId.HasValue)
            {
                // ✅ SỬA: Lọc theo TASK thuộc phòng (không phải NV thuộc phòng)
                // → Khi NV bị gỡ, báo cáo của họ cho task phòng này VẪN hiển thị
                // → Cấp trên kiểm tra vẫn thấy đầy đủ lịch sử
                var taskIdsInUnit = await _taskRepo.Query()
                    .Where(t => t.UnitId == unitId.Value && !t.IsDeleted)
                    .Select(t => t.Id)
                    .ToListAsync();

                query = query.Where(p => taskIdsInUnit.Contains(p.TaskId));
            }

            var total = await query.CountAsync();
            var progresses = await query
                .OrderByDescending(p => p.UpdatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var userIds = progresses.Select(p => p.UserId).Distinct().ToList();
            var taskIds = progresses.Select(p => p.TaskId).Distinct().ToList();
            var progressIds = progresses.Select(p => p.Id).ToList();

            var users = await _userRepo.Query()
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            var tasks = await _taskRepo.Query()
                .Where(t => taskIds.Contains(t.Id))
                .ToListAsync();

            var files = await _uploadRepo.Query()
                .Where(f => f.ProgressId.HasValue && progressIds.Contains(f.ProgressId.Value))
                .ToListAsync();

            var reviews = await _reviewRepo.Query()
                .Where(r => progressIds.Contains(r.ProgressId))
                .ToListAsync();

            var dtos = progresses.Select(p => {
                var dto = _mapper.Map<ProgressDto>(p);
                var user = users.FirstOrDefault(u => u.Id == p.UserId);
                var task = tasks.FirstOrDefault(t => t.Id == p.TaskId);

                dto.UserFullName = user?.FullName ?? "—";
                dto.UserEmployeeCode = user?.EmployeeCode ?? "—";
                dto.TaskTitle = task?.Title ?? "—";
                
                dto.Files = files
                    .Where(f => f.ProgressId == dto.Id)
                    .Select(f => new UploadFileDto {
                        Id = f.Id,
                        FileName = f.FileName,
                        FilePath = f.FilePath,
                        CreatedAt = f.CreatedAt,
                        ProgressId = f.ProgressId
                    }).ToList();

                var review = reviews.FirstOrDefault(r => r.ProgressId == p.Id);
                dto.ReviewComment = review?.Comment;

                return dto;
            }).ToList();

            return new { total, data = dtos };
        }

        public async Task<List<ProgressDto>> GetByTaskAsync(Guid taskId, Guid userId)
        {
            var task = await _taskRepo.GetByIdAsync(taskId);
            if (task == null) return new List<ProgressDto>();

            var progresses = await _repo.Query().Where(p => p.TaskId == taskId).OrderByDescending(p => p.UpdatedAt).ToListAsync();
            
            var userIds = progresses.Select(p => p.UserId).Distinct().ToList();
            var users = await _userRepo.Query().Where(u => userIds.Contains(u.Id)).ToListAsync();

            var progressIds = progresses.Select(p => p.Id).ToList();
            var files = await _uploadRepo.Query().Where(f => f.ProgressId.HasValue && progressIds.Contains(f.ProgressId.Value)).ToListAsync();
            var reviews = await _reviewRepo.Query().Where(r => progressIds.Contains(r.ProgressId)).ToListAsync();

            var dtos = _mapper.Map<List<ProgressDto>>(progresses);
            foreach (var dto in dtos)
            {
                var user = users.FirstOrDefault(u => u.Id == dto.UserId);
                dto.UserFullName = user?.FullName ?? "—";
                dto.UserEmployeeCode = user?.EmployeeCode ?? "—";
                dto.TaskTitle = task.Title;
                
                dto.Files = files
                    .Where(f => f.ProgressId == dto.Id)
                    .Select(f => new UploadFileDto {
                        Id = f.Id,
                        FileName = f.FileName,
                        FilePath = f.FilePath,
                        CreatedAt = f.CreatedAt,
                        ProgressId = f.ProgressId
                    }).ToList();

                var review = reviews.FirstOrDefault(r => r.ProgressId == dto.Id);
                dto.ReviewComment = review?.Comment;
            }

            return dtos;
        }

        /// <summary>
        /// Lịch sử cá nhân: Trả về TẤT CẢ báo cáo của user (không lọc phòng ban)
        /// Dùng cho: NV/Manager xem lại thành tích sau khi chuyển phòng/đổi chức
        /// </summary>
        public async Task<object> GetMyHistory(Guid userId, int page, int size)
        {
            // Lấy báo cáo do chính mình nộp (Nhân viên) HOẶC báo cáo nộp cho Task do mình tạo (Trưởng phòng)
            var createdTaskIds = await _taskRepo.Query()
                .Where(t => t.CreatedBy == userId)
                .Select(t => t.Id)
                .ToListAsync();

            var query = _repo.Query().Where(p => p.UserId == userId || createdTaskIds.Contains(p.TaskId));

            var total = await query.CountAsync();
            var progresses = await query
                .OrderByDescending(p => p.UpdatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var taskIds = progresses.Select(p => p.TaskId).Distinct().ToList();
            var progressIds = progresses.Select(p => p.Id).ToList();

            var tasks = await _taskRepo.Query()
                .Where(t => taskIds.Contains(t.Id))
                .ToListAsync();

            // Lấy tên phòng ban cho mỗi task
            var unitIds = tasks.Where(t => t.UnitId.HasValue).Select(t => t.UnitId.Value).Distinct().ToList();
            var unitNames = await _unitRepo.Query()
                .Where(u => unitIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            var files = await _uploadRepo.Query()
                .Where(f => f.ProgressId.HasValue && progressIds.Contains(f.ProgressId.Value))
                .ToListAsync();

            var reviews = await _reviewRepo.Query()
                .Where(r => progressIds.Contains(r.ProgressId))
                .ToListAsync();

            var submitterIds = progresses.Select(p => p.UserId).Distinct().ToList();
            var submitters = await _userRepo.Query()
                .Where(u => submitterIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u);

            var dtos = progresses.Select(p =>
            {
                var dto = _mapper.Map<ProgressDto>(p);
                var task = tasks.FirstOrDefault(t => t.Id == p.TaskId);

                var submitter = submitters.ContainsKey(p.UserId) ? submitters[p.UserId] : null;
                dto.UserFullName = submitter?.FullName ?? "—";
                dto.UserEmployeeCode = submitter?.EmployeeCode ?? "—";
                dto.TaskTitle = task?.Title ?? "—";
                dto.UnitName = task?.UnitId != null && unitNames.ContainsKey(task.UnitId.Value)
                    ? unitNames[task.UnitId.Value] : "—";

                dto.Files = files
                    .Where(f => f.ProgressId == dto.Id)
                    .Select(f => new UploadFileDto
                    {
                        Id = f.Id,
                        FileName = f.FileName,
                        FilePath = f.FilePath,
                        CreatedAt = f.CreatedAt,
                        ProgressId = f.ProgressId
                    }).ToList();

                var review = reviews.FirstOrDefault(r => r.ProgressId == p.Id);
                dto.ReviewComment = review?.Comment;

                return dto;
            }).ToList();

            return new { total, data = dtos };
        }
    }
}
