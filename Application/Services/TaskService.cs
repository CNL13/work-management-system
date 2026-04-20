using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;
using TaskItem = WorkManagementSystem.Domain.Entities.TaskItem;

namespace WorkManagementSystem.Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<UserUnit> _userUnitRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<Unit> _unitRepo; // ✅ MỚI
        private readonly IGenericRepository<TaskHistory> _historyRepo;
        private readonly IGenericRepository<UploadFile> _uploadRepo;
        private readonly IGenericRepository<SubTask> _subTaskRepo;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        public TaskService(
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<UserUnit> userUnitRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<Unit> unitRepo, // ✅ MỚI
            IGenericRepository<TaskHistory> historyRepo,
            IGenericRepository<UploadFile> uploadRepo,
            IGenericRepository<SubTask> subTaskRepo,
            INotificationService notificationService,
            IMapper mapper)
        {
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _userUnitRepo = userUnitRepo;
            _userRepo = userRepo;
            _unitRepo = unitRepo; // ✅ MỚI
            _historyRepo = historyRepo;
            _uploadRepo = uploadRepo;
            _subTaskRepo = subTaskRepo;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        public async Task<Guid?> GetManagerUnitId(Guid managerId)
        {
            var manager = await _userRepo.GetByIdAsync(managerId);
            return manager?.UnitId;
        }

        private async Task<bool> IsAuthorizedToModifyTask(TaskItem task, Guid userId, bool requireManagerOrCreator = false)
        {
            if (task.CreatedBy == userId) return true;
            var user = await _userRepo.GetByIdAsync(userId);
            if (user != null && (user.Role == "Admin" || user.Role == "Manager")) return true;
            if (requireManagerOrCreator) return false;
            
            return await _assigneeRepo.Query().AnyAsync(a => a.TaskId == task.Id && a.UserId == userId);
        }

        public async Task<TaskDto> Create(CreateTaskDto dto, Guid userId)
        {
            var task = _mapper.Map<TaskItem>(dto);
            task.Id = Guid.NewGuid();
            task.CreatedBy = userId;
            task.CreatedAt = DateTime.UtcNow;
            
            // ✅ MỚI: Tự động gán Task vào Unit của người tạo
            var creator = await _userRepo.GetByIdAsync(userId);
            if (creator != null) task.UnitId = creator.UnitId;

            task.DueDate = dto.DueDate;
            task.EstimatedHours = Math.Max(0, dto.EstimatedHours); // ✅ FIX BUG-10: Chặn giá trị âm
            await _taskRepo.AddAsync(task);

            if (dto.UserIds != null)
            {
                foreach (var uid in dto.UserIds)
                {
                    await _assigneeRepo.AddAsync(new TaskAssignee { Id = Guid.NewGuid(), TaskId = task.Id, UserId = uid });
                    await _notificationService.AddNotification(uid, $"Khẩn cấp: Bạn vừa được giao một công việc mới - '{task.Title}'");
                }
            }

            if (dto.UnitIds != null)
                foreach (var unitId in dto.UnitIds)
                    await _assigneeRepo.AddAsync(new TaskAssignee
                    { Id = Guid.NewGuid(), TaskId = task.Id, UnitId = unitId });

            await _taskRepo.SaveAsync();
            return _mapper.Map<TaskDto>(task);
        }

        public async Task<object> Get(
            string keyword, int page, int size,
            string? status,
            Guid currentUserId, // ✅ THÊM currentUserId
            Guid? userId = null,
            Guid? unitId = null)
        {
            page = page <= 0 ? 1 : page;
            size = size <= 0 ? 10 : size;

            var query = _taskRepo.Query();

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(x => x.Title.Contains(keyword));

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatusEnum>(status, true, out var statusEnum))
                query = query.Where(x => x.Status == statusEnum);

            if (unitId.HasValue)
            {
                var userIdsInUnit = await _userUnitRepo.Query()
                    .Where(uu => uu.UnitId == unitId.Value)
                    .Select(uu => uu.UserId)
                    .ToListAsync();

                query = query.Where(x => 
                    x.UnitId == unitId.Value || 
                    (x.UnitId == null && _assigneeRepo.Query().Any(a => a.TaskId == x.Id && (a.UnitId == unitId.Value || (a.UserId.HasValue && userIdsInUnit.Contains(a.UserId.Value)))))
                );
            }
            else
            {
                var user = await _userRepo.GetByIdAsync(currentUserId);
                
                var myTaskIds = await _assigneeRepo.Query()
                    .Where(a => a.UserId == currentUserId)
                    .Select(a => a.TaskId)
                    .ToListAsync();

                if (user != null && user.UnitId.HasValue)
                {
                    var userUnitId = user.UnitId.Value;
                    // ✅ SỬA: Nhân viên chỉ thấy task được giao TRONG PHÒNG HIỆN TẠI
                    // Task phòng cũ vẫn giữ nguyên trong DB (bảo toàn lịch sử KPI/khen thưởng)
                    query = query.Where(x => x.UnitId == userUnitId && myTaskIds.Contains(x.Id));
                }
                else
                {
                    query = query.Where(x => myTaskIds.Contains(x.Id));
                }
            }

            var total = await query.CountAsync();
            var data = await query
                .OrderBy(x => x.OrderIndex)
                .ThenByDescending(x => x.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var taskIds = data.Select(x => x.Id).ToList();
            var assignees = await _assigneeRepo.Query()
                .Where(a => taskIds.Contains(a.TaskId) && a.UserId.HasValue)
                .Join(_userRepo.Query(),
                    a => a.UserId,
                    u => u.Id,
                    (a, u) => new { a.TaskId, u.Id, u.FullName, u.EmployeeCode })
                .ToListAsync();

            var taskDtos = _mapper.Map<List<TaskDto>>(data);
            foreach (var dto in taskDtos)
            {
                dto.Assignees = assignees
                    .Where(a => a.TaskId == dto.Id)
                    .Select(a => new TaskAssigneeDto 
                    { 
                        Id = a.Id, 
                        FullName = a.FullName ?? "—", 
                        EmployeeCode = a.EmployeeCode ?? "—" 
                    })
                    .ToList();
            }

            // ✅ MỚI: Lấy danh sách File Minh chứng cho Task
            var taskFiles = await _uploadRepo.Query()
                .Where(f => f.TaskId.HasValue && taskIds.Contains(f.TaskId.Value))
                .ToListAsync();

            var taskSubTasks = await _subTaskRepo.Query()
                .Where(s => taskIds.Contains(s.TaskId))
                .ToListAsync();

            // ✅ MỚI: Lấy thông tin Tên phòng ban và Tên người tạo cho Task
            var unitMap = await _unitRepo.Query()
                .Where(u => taskIds.Any())
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            var creatorIds = data.Select(x => x.CreatedBy).Distinct().ToList();
            var creatorMap = await _userRepo.Query()
                .Where(u => creatorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

            foreach (var dto in taskDtos)
            {
                var originalData = data.First(x => x.Id == dto.Id);
                dto.EstimatedHours = originalData.EstimatedHours;
                dto.ActualHours = originalData.ActualHours;
                dto.UnitId = originalData.UnitId;
                
                if (dto.UnitId.HasValue && unitMap.TryGetValue(dto.UnitId.Value, out var uName))
                {
                    dto.UnitName = uName;
                }

                if (creatorMap.TryGetValue(originalData.CreatedBy, out var cName))
                {
                    dto.CreatedByName = cName;
                }

                dto.Files = taskFiles
                    .Where(f => f.TaskId == dto.Id)
                    .Select(f => new UploadFileDto
                    {
                        Id = f.Id,
                        FileName = f.FileName,
                        FilePath = f.FilePath,
                        CreatedAt = f.CreatedAt,
                        TaskId = f.TaskId
                    }).ToList();

                dto.SubTasks = _mapper.Map<List<SubTaskDto>>(taskSubTasks.Where(s => s.TaskId == dto.Id));
            }

            return new { total, page, size, data = taskDtos };
        }

        // ✅ SỬA: Thêm Audit Log khi cập nhật Task và Authorization
        public async Task<TaskDto> Update(Guid id, CreateTaskDto dto, Guid changedBy)
        {
            var task = await _taskRepo.GetByIdAsync(id)
                ?? throw new Exception("Task not found");

            if (!await IsAuthorizedToModifyTask(task, changedBy, true))
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa công việc này.");


            // ✅ MỚI: Ghi lại lịch sử thay đổi
            if (task.Title != dto.Title)
                await _historyRepo.AddAsync(new TaskHistory
                {
                    Id = Guid.NewGuid(),
                    TaskId = id,
                    ChangedBy = changedBy,
                    FieldName = "Title",
                    OldValue = task.Title,
                    NewValue = dto.Title
                });

            if (task.DueDate != dto.DueDate)
                await _historyRepo.AddAsync(new TaskHistory
                {
                    Id = Guid.NewGuid(),
                    TaskId = id,
                    ChangedBy = changedBy,
                    FieldName = "DueDate",
                    OldValue = task.DueDate?.ToString("dd/MM/yyyy") ?? "Không có",
                    NewValue = dto.DueDate?.ToString("dd/MM/yyyy") ?? "Không có"
                });

            if (task.Description != dto.Description)
                await _historyRepo.AddAsync(new TaskHistory
                {
                    Id = Guid.NewGuid(),
                    TaskId = id,
                    ChangedBy = changedBy,
                    FieldName = "Description",
                    OldValue = task.Description,
                    NewValue = dto.Description
                });

            task.Title = dto.Title;
            task.Description = dto.Description;
            task.DueDate = dto.DueDate;
            task.EstimatedHours = Math.Max(0, dto.EstimatedHours); // ✅ Chặn giá trị âm
            _taskRepo.Update(task);
            await _taskRepo.SaveAsync();

            var assignedUserIds = await _assigneeRepo.Query()
                .Where(a => a.TaskId == id && a.UserId.HasValue)
                .Select(a => a.UserId.Value)
                .ToListAsync();
            foreach (var uid in assignedUserIds)
            {
                await _notificationService.AddNotification(uid, $"Cập nhật: Công việc '{task.Title}' đã có thay đổi nội dung/thời hạn");
            }

            return _mapper.Map<TaskDto>(task);
        }

        public async Task UpdateStatus(Guid id, string status, Guid userId)
        {
            var task = await _taskRepo.GetByIdAsync(id)
                ?? throw new Exception("Task not found");

            if (!await IsAuthorizedToModifyTask(task, userId))
                throw new UnauthorizedAccessException("Bạn không có quyền đổi trạng thái công việc này.");

            if (Enum.TryParse<TaskStatusEnum>(status, true, out var statusEnum))
            {
                // ✅ MỚI: State Machine & Role Validation
                if (statusEnum == TaskStatusEnum.Approved || statusEnum == TaskStatusEnum.Rejected)
                {
                    var user = await _userRepo.GetByIdAsync(userId);
                    if (user == null || (user.Role != "Admin" && user.Role != "Manager" && task.CreatedBy != userId))
                        throw new Exception("Trạng thái này chỉ do Quản lý hoặc Người tạo phê duyệt thông qua chức năng báo cáo.");
                }
                
                if (task.Status == TaskStatusEnum.Approved && statusEnum != TaskStatusEnum.Approved)
                {
                    var user = await _userRepo.GetByIdAsync(userId);
                    if (user == null || (user.Role != "Admin" && user.Role != "Manager" && task.CreatedBy != userId))
                        throw new Exception("Công việc đã hoàn tất, chỉ Quản lý/Admin mới có quyền hủy bỏ trạng thái này.");
                }

                var oldStatus = task.Status.ToString();
                task.Status = statusEnum;
                _taskRepo.Update(task);

                await _historyRepo.AddAsync(new TaskHistory
                {
                    Id = Guid.NewGuid(),
                    TaskId = id,
                    ChangedBy = userId,
                    FieldName = "Status",
                    OldValue = oldStatus,
                    NewValue = status
                });
                await _taskRepo.SaveAsync();
            }
        }

        // ✅ SỬA: Soft delete thay vì xóa cứng và Authorization
        public async Task Delete(Guid id, Guid userId)
        {
            var task = await _taskRepo.GetByIdAsync(id)
                ?? throw new Exception("Task not found");

            if (!await IsAuthorizedToModifyTask(task, userId, true))
                throw new UnauthorizedAccessException("Bạn không có quyền xóa công việc này.");

            task.IsDeleted = true;  // ✅ Soft delete
            _taskRepo.Update(task);
            await _taskRepo.SaveAsync();
        }

        public async Task RemindTask(Guid taskId, Guid reminderId)
        {
            var task = await _taskRepo.GetByIdAsync(taskId)
                ?? throw new Exception("Task not found");

            if (!await IsAuthorizedToModifyTask(task, reminderId, true))
                throw new UnauthorizedAccessException("Bạn không có quyền đôn đốc công việc này.");

            var assignedUserIds = await _assigneeRepo.Query()

                .Where(a => a.TaskId == taskId && a.UserId.HasValue)
                .Select(a => a.UserId.Value)
                .ToListAsync();

            if (!assignedUserIds.Any())
                throw new Exception("Không có nhân viên nào được giao công việc này.");

            var reminderUser = await _userRepo.GetByIdAsync(reminderId);
            var reminderName = reminderUser?.FullName ?? "Quản lý";

            foreach (var userId in assignedUserIds)
            {
                await _notificationService.AddNotification(userId, $"Quản lý {reminderName} đã đôn đốc bạn về công việc: {task.Title}. Hãy khẩn trương hoàn thành!");
            }

            await _historyRepo.AddAsync(new TaskHistory
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                ChangedBy = reminderId,
                FieldName = "Remind",
                OldValue = "N/A",
                NewValue = "Đã gửi nhắc nhở đôn đốc tiến độ"
            });
            await _historyRepo.SaveAsync();
        }

        // ✅ MỚI: Kéo thả thứ tự Kanban
        public async Task Reorder(Guid id, int newIndex, Guid userId)
        {
            var task = await _taskRepo.GetByIdAsync(id) ?? throw new Exception("Task not found");
            if (!await IsAuthorizedToModifyTask(task, userId)) throw new UnauthorizedAccessException();
            
            task.OrderIndex = newIndex;
            _taskRepo.Update(task);
            await _taskRepo.SaveAsync();
        }

        public async Task<TaskDto> GetByIdAsync(Guid id, Guid userId)
        {
            var task = await _taskRepo.GetByIdAsync(id) ?? throw new Exception("Task not found");
            if (!await IsAuthorizedToModifyTask(task, userId)) throw new UnauthorizedAccessException();

            var dto = _mapper.Map<TaskDto>(task);

            // Fetch assignees
            var assigneeQuery = await _assigneeRepo.Query()
                .Where(a => a.TaskId == id && a.UserId.HasValue)
                .Join(_userRepo.Query(), a => a.UserId, u => u.Id, (a, u) => new TaskAssigneeDto
                {
                    Id = a.Id,
                    FullName = u.FullName,
                    EmployeeCode = u.EmployeeCode
                }).ToListAsync();
            dto.Assignees = assigneeQuery;

            // Fetch subtasks
            var subtasks = await _subTaskRepo.Query().Where(s => s.TaskId == id).ToListAsync();
            dto.SubTasks = _mapper.Map<List<SubTaskDto>>(subtasks);

            // Fetch files
            dto.Files = await _uploadRepo.Query().Where(f => f.TaskId == id).Select(f => new UploadFileDto
            {
                Id = f.Id,
                FileName = f.FileName,
                FilePath = f.FilePath,
                CreatedAt = f.CreatedAt,
                TaskId = f.TaskId
            }).ToListAsync();

            // Fetch UnitName & CreatedBy
            if (task.UnitId.HasValue)
            {
                var unit = await _unitRepo.GetByIdAsync(task.UnitId.Value);
                dto.UnitId = task.UnitId;
                dto.UnitName = unit?.Name;
            }

            var creator = await _userRepo.GetByIdAsync(task.CreatedBy);
            dto.CreatedByName = creator?.FullName;

            return dto;
        }

        public async Task<List<TaskHistory>> GetHistoryAsync(Guid taskId, Guid userId)
        {
            var task = await _taskRepo.GetByIdAsync(taskId) ?? throw new Exception("Task not found");
            if (!await IsAuthorizedToModifyTask(task, userId)) throw new UnauthorizedAccessException();

            return await _historyRepo.Query()
                .Where(h => h.TaskId == taskId)
                .OrderBy(h => h.ChangedAt)
                .ToListAsync();
        }
    }
}
