using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using Microsoft.AspNetCore.SignalR;
using WorkManagementSystem.API.Hubs;

namespace WorkManagementSystem.Application.Services
{
    public class SubTaskService : ISubTaskService
    {
        private readonly IGenericRepository<SubTask> _repo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IHubContext<DiscussionHub> _hubContext;
        private readonly IMapper _mapper;

        public SubTaskService(
            IGenericRepository<SubTask> repo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IHubContext<DiscussionHub> hubContext,
            IMapper mapper)
        {
            _repo = repo;
            _taskRepo = taskRepo;
            _userRepo = userRepo;
            _assigneeRepo = assigneeRepo;
            _hubContext = hubContext;
            _mapper = mapper;
        }

        private async Task<bool> IsAuthorized(Guid taskId, Guid userId, bool requireManagerOrCreator = false)
        {
            var task = await _taskRepo.GetByIdAsync(taskId);
            if (task == null) return false;
            if (task.CreatedBy == userId) return true;

            var user = await _userRepo.GetByIdAsync(userId);
            if (user != null && (user.Role == "Admin" || user.Role == "Manager")) return true;
            if (requireManagerOrCreator) return false;

            return await _assigneeRepo.Query().AnyAsync(a => a.TaskId == taskId && a.UserId == userId);
        }

        public async Task<SubTaskDto> AddSubTask(CreateSubTaskDto dto, Guid userId)
        {
            if (!await IsAuthorized(dto.TaskId, userId, true))
                throw new UnauthorizedAccessException("Chỉ Quản lý hoặc Người tạo mới được quyền thêm công việc con cho nhiệm vụ này.");

            var exists = await _repo.Query().AnyAsync(s => s.TaskId == dto.TaskId && s.Title == dto.Title);
            if (exists) throw new Exception("Công việc con này đã tồn tại trong nhiệm vụ!");

            var subTask = new SubTask
            {
                Id = Guid.NewGuid(),
                TaskId = dto.TaskId,
                Title = dto.Title,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(subTask);
            await _repo.SaveAsync();

            var result = _mapper.Map<SubTaskDto>(subTask);
            await _hubContext.Clients.Group(dto.TaskId.ToString()).SendAsync("ReceiveSubTaskAdded", result);
            return result;
        }

        public async Task ToggleSubTask(Guid id, Guid userId)
        {
            var subTask = await _repo.GetByIdAsync(id) ?? throw new Exception("Sub-task not found");
            if (!await IsAuthorized(subTask.TaskId, userId))
                throw new UnauthorizedAccessException("Bạn không có quyền cập nhật công việc con này.");

            subTask.IsCompleted = !subTask.IsCompleted;
            _repo.Update(subTask);
            await _repo.SaveAsync();

            await _hubContext.Clients.Group(subTask.TaskId.ToString()).SendAsync("ReceiveSubTaskToggled", id, subTask.IsCompleted);
        }

        public async Task Delete(Guid id, Guid userId)
        {
            var subTask = await _repo.GetByIdAsync(id) ?? throw new Exception("Sub-task not found");
            if (!await IsAuthorized(subTask.TaskId, userId, true))
                throw new UnauthorizedAccessException("Chỉ Quản lý hoặc Người tạo mới được quyền xóa công việc con.");

            var taskId = subTask.TaskId;
            _repo.Delete(subTask);
            await _repo.SaveAsync();

            await _hubContext.Clients.Group(taskId.ToString()).SendAsync("ReceiveSubTaskDeleted", id);
        }

        public async Task<List<SubTaskDto>> GetSubTasks(Guid taskId)
        {
            var list = await _repo.Query().Where(s => s.TaskId == taskId).ToListAsync();
            return _mapper.Map<List<SubTaskDto>>(list);
        }
    }
}
