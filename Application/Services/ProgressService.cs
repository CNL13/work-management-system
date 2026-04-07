using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class ProgressService : IProgressService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public ProgressService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<ProgressDto> Update(CreateProgressDto dto)  // Task → Task<ProgressDto>
        {
            var progress = _mapper.Map<Progress>(dto);
            progress.Id = Guid.NewGuid();
            progress.Status = TaskStatus.Submitted;
            _context.Progresses.Add(progress);
            await _context.SaveChangesAsync();
            return _mapper.Map<ProgressDto>(progress);  // ✅ thêm dòng này
        }
        public async Task<object> GetAll(int page, int size)
        {
            var total = await _context.Progresses.CountAsync();
            var data = await _context.Progresses
                .Skip((page - 1) * size).Take(size).ToListAsync();
            return new { total, data };
        }
    }
}