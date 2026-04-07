using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace WorkManagementSystem.Application.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        public UserService(AppDbContext context) { _context = context; }

        public async Task<List<UserDto>> GetAll()
            => await _context.Users
                .Select(u => new UserDto { Id = u.Id, Username = u.Username, Role = u.Role })
                .ToListAsync();

        public async Task<UserDto> Update(Guid id, UpdateUserDto dto)
        {
            var user = await _context.Users.FindAsync(id)
                ?? throw new Exception("User not found");
            user.Role = dto.Role;
            await _context.SaveChangesAsync();
            return new UserDto { Id = user.Id, Username = user.Username, Role = user.Role };
        }

        public async Task Delete(Guid id)
        {
            var user = await _context.Users.FindAsync(id)
                ?? throw new Exception("User not found");
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}