using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;

// alias tránh trùng
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;
using TaskItem = WorkManagementSystem.Domain.Entities.TaskItem;

namespace WorkManagementSystem.Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public TaskService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        // ================= CREATE =================
        public async Task<TaskDto> Create(CreateTaskDto dto, Guid userId)
        {
            var task = _mapper.Map<TaskItem>(dto);

            task.Id = Guid.NewGuid();
            task.CreatedBy = userId;
            task.CreatedAt = DateTime.UtcNow;

            _context.Tasks.Add(task);

            // assign user
            if (dto.UserIds != null)
            {
                foreach (var uid in dto.UserIds)
                {
                    _context.TaskAssignees.Add(new TaskAssignee
                    {
                        Id = Guid.NewGuid(),
                        TaskId = task.Id,
                        UserId = uid
                    });
                }
            }

            // assign unit
            if (dto.UnitIds != null)
            {
                foreach (var unitId in dto.UnitIds)
                {
                    _context.TaskAssignees.Add(new TaskAssignee
                    {
                        Id = Guid.NewGuid(),
                        TaskId = task.Id,
                        UnitId = unitId
                    });
                }
            }

            await _context.SaveChangesAsync();

            return _mapper.Map<TaskDto>(task);
        }

        // ================= GET (SEARCH + FILTER + PAGING) =================
        public async Task<object> Get(string keyword, int page, int size, string? status)
        {
            // fix paging
            page = page <= 0 ? 1 : page;
            size = size <= 0 ? 10 : size;

            var query = _context.Tasks.AsQueryable();

            // search
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(x => x.Title.Contains(keyword));
            }

            // filter status
            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatusEnum>(status, true, out var statusEnum))
            {
                query = query.Where(x => x.Status == statusEnum);
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreatedAt) // QUAN TRỌNG
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var result = _mapper.Map<List<TaskDto>>(data);

            return new
            {
                total,
                page,
                size,
                data = result
            };
        }

        // ================= UPDATE =================
        public async Task<TaskDto> Update(Guid id, CreateTaskDto dto)
        {
            var task = await _context.Tasks.FindAsync(id)
                ?? throw new Exception("Task not found");

            task.Title = dto.Title;
            task.Description = dto.Description;

            await _context.SaveChangesAsync();

            return _mapper.Map<TaskDto>(task);
        }

        // ================= DELETE =================
        public async Task Delete(Guid id)
        {
            var task = await _context.Tasks.FindAsync(id)
                ?? throw new Exception("Task not found");

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }
}