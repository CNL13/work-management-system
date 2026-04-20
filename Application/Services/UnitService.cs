using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;

namespace WorkManagementSystem.Application.Services
{
    public class UnitService : IUnitService
    {
        private readonly IGenericRepository<Unit> _repo;
        private readonly IGenericRepository<UserUnit> _userUnitRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IMapper _mapper;

        public UnitService(
            IGenericRepository<Unit> repo,
            IGenericRepository<UserUnit> userUnitRepo,
            IGenericRepository<User> userRepo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IMapper mapper)
        {
            _repo = repo;
            _userUnitRepo = userUnitRepo;
            _userRepo = userRepo;
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _mapper = mapper;
        }

        public async Task<List<UnitDto>> GetAll()
            => _mapper.Map<List<UnitDto>>(await _repo.Query().ToListAsync());

        public async Task<UnitDto?> GetMyUnit(Guid userId)
        {
            var userUnit = await _userUnitRepo.Query()
                .Include(x => x.Unit)
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (userUnit != null)
                return _mapper.Map<UnitDto>(userUnit.Unit);

            var user = await _userRepo.GetByIdAsync(userId);
            if (user?.UnitId != null)
            {
                var unit = await _repo.GetByIdAsync(user.UnitId.Value);
                return _mapper.Map<UnitDto>(unit);
            }

            return null;
        }

        public async Task<List<UserDto>> GetUsers(Guid unitId)
        {
            // Lấy ID từ bảng liên kết
            var userIdsFromMapping = await _userUnitRepo.Query()
                .Where(x => x.UnitId == unitId)
                .Select(x => x.UserId)
                .ToListAsync();

            // Lấy user từ cả 2 nguồn: UnitId trực tiếp HOẶC nằm trong danh sách Mapping
            var users = await _userRepo.Query()
                .Where(u => (u.UnitId == unitId || userIdsFromMapping.Contains(u.Id)) 
                            && u.IsApproved 
                            && !u.IsDeleted)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName ?? "—",
                    EmployeeCode = u.EmployeeCode ?? "—",
                    Role = u.Role,
                    UnitId = u.UnitId ?? unitId, // ✅ TỰ CHỮA LÀNH: Ưu tiên trả về UnitId hiện tại nếu hồ sơ đang null
                    IsApproved = u.IsApproved,
                    PhoneNumber = u.PhoneNumber 
                })
                .ToListAsync();

            return users;
        }

        public async Task<UnitDto> Create(CreateUnitDto dto)
        {
            var exists = await _repo.Query().AnyAsync(u => u.Name == dto.Name);
            if (exists) throw new Exception("Tên phòng ban đã tồn tại!");

            var unit = new Unit { Id = Guid.NewGuid(), Name = dto.Name };
            await _repo.AddAsync(unit);
            await _repo.SaveAsync();
            return _mapper.Map<UnitDto>(unit);
        }

        public async Task<UnitDto> Update(Guid id, CreateUnitDto dto)
        {
            var exists = await _repo.Query().AnyAsync(u => u.Name == dto.Name && u.Id != id);
            if (exists) throw new Exception("Tên phòng ban đã tồn tại!");

            var unit = await _repo.GetByIdAsync(id)
                ?? throw new Exception("Unit not found");
            unit.Name = dto.Name;
            _repo.Update(unit);
            await _repo.SaveAsync();
            return _mapper.Map<UnitDto>(unit);
        }

        // ✅ SỬA: Soft delete thay vì xóa cứng
        public async Task Delete(Guid id)
        {
            var unit = await _repo.GetByIdAsync(id)
                ?? throw new Exception("Unit not found");

            // ✅ CHỐT CHẶN: Kiểm tra xem phòng ban có còn nhân sự không
            var hasMembers = await _userUnitRepo.Query().AnyAsync(x => x.UnitId == id);
            if (hasMembers) 
            {
                throw new Exception("Không thể xóa! Phòng ban này vẫn đang có nhân sự. Vui lòng luân chuyển toàn bộ Quản lý và Nhân viên sang phòng khác hoặc gỡ tư cách thành viên của họ trước.");
            }

            unit.IsDeleted = true;  // ✅ Soft delete
            _repo.Update(unit);
            await _repo.SaveAsync();
        }

        public async Task AddMember(Guid unitId, Guid userId)
        {
            var exists = await _userUnitRepo.Query()
                .AnyAsync(x => x.UnitId == unitId && x.UserId == userId);
            if (exists) throw new Exception("Thành viên đã thuộc đơn vị này!");

            // 1. Thêm vào bảng liên kết Mapping
            await _userUnitRepo.AddAsync(new UserUnit
            {
                Id = Guid.NewGuid(),
                UnitId = unitId,
                UserId = userId
            });

            // 2. ĐỒNG BỘ: Cập nhật luôn UnitId trực tiếp trên bảng User
            var user = await _userRepo.GetByIdAsync(userId);
            if (user != null)
            {
                // ✅ SỬA: Reset JoinedUnitAt khi gia nhập phòng mới
                // KPI hiện tại bắt đầu tính từ mốc này (lịch sử TaskAssignee/Progress vẫn giữ nguyên)
                if (user.UnitId != unitId)
                {
                    user.JoinedUnitAt = DateTime.UtcNow;
                }
                user.UnitId = unitId;
                _userRepo.Update(user);
                await _userRepo.SaveAsync();
            }

            await _userUnitRepo.SaveAsync();
        }

        public async Task RemoveMember(Guid unitId, Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new Exception("User not found");

            // ✅ CHỐT CHẶN: Nhân viên phải hoàn thành và được duyệt hết việc trong đơn vị mới được gỡ
            if (user.Role == "User")
            {
                var pendingTasks = await _taskRepo.Query()
                    .Where(t => t.UnitId == unitId && !t.IsDeleted && t.Status != Domain.Enums.TaskStatus.Approved)
                    .Join(_assigneeRepo.Query().Where(a => a.UserId == userId),
                        t => t.Id,
                        a => a.TaskId,
                        (t, a) => t.Title)
                    .ToListAsync();

                if (pendingTasks.Any())
                {
                    throw new Exception($"Không thể gỡ nhân viên. Còn {pendingTasks.Count} công việc chưa hoàn thành: {string.Join(", ", pendingTasks)}. Vui lòng vào trang Nhiệm vụ, bàn giao hoặc gỡ các công việc này cho NV khác trước khi gỡ khỏi phòng.");
                }
            }
            // XÓA BỎ: Không chặn Manager bị gỡ khỏi phòng nếu phòng cũ còn việc.

            // 1. Xóa trong bảng liên kết (Mapping Table)
            var userUnit = await _userUnitRepo.Query()
                .FirstOrDefaultAsync(x => x.UnitId == unitId && x.UserId == userId);

            if (userUnit != null)
            {
                _userUnitRepo.Delete(userUnit);
            }

            // 2. Xóa UnitId trực tiếp trên User (nếu có)
            if (user != null && user.UnitId == unitId)
            {
                user.UnitId = null;
                _userRepo.Update(user);
            }

            // 3. Nếu cả 2 đều không có thì mới báo lỗi
            if (userUnit == null && (user == null || user.UnitId != null)) 
            {
                // Note: Logic này hơi lắt léo, chỉ báo lỗi nếu thực sự không tìm thấy liên kết nào
                if (userUnit == null && (user == null || user.UnitId == null))
                     throw new Exception("Không tìm thấy thành viên thuộc đơn vị này!");
            }

            await _userRepo.SaveAsync(); // Save cho cả 2 repo nếu dùng chung DbContext
            await _userUnitRepo.SaveAsync();
        }
    }
}
