using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace WorkManagementSystem.Application.Services
{
    public class UnitService : IUnitService
    {
        private readonly AppDbContext _context;
        public UnitService(AppDbContext context) { _context = context; }

        public async Task<List<UnitDto>> GetAll()
            => await _context.Units
                .Select(u => new UnitDto { Id = u.Id, Name = u.Name })
                .ToListAsync();

        public async Task<UnitDto> Create(CreateUnitDto dto)
        {
            var unit = new Unit { Id = Guid.NewGuid(), Name = dto.Name };
            _context.Units.Add(unit);
            await _context.SaveChangesAsync();
            return new UnitDto { Id = unit.Id, Name = unit.Name };
        }

        public async Task<UnitDto> Update(Guid id, CreateUnitDto dto)
        {
            var unit = await _context.Units.FindAsync(id)
                ?? throw new Exception("Unit not found");
            unit.Name = dto.Name;
            await _context.SaveChangesAsync();
            return new UnitDto { Id = unit.Id, Name = unit.Name };
        }

        public async Task Delete(Guid id)
        {
            var unit = await _context.Units.FindAsync(id)
                ?? throw new Exception("Unit not found");
            _context.Units.Remove(unit);
            await _context.SaveChangesAsync();
        }
    }
}