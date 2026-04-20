using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Repositories;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IGenericRepository<User> _repo;
        private readonly IGenericRepository<UserUnit> _userUnitRepo;
        private readonly IGenericRepository<TaskItem> _taskRepo;
        private readonly IGenericRepository<TaskAssignee> _assigneeRepo;
        private readonly IGenericRepository<Progress> _progressRepo;
        private readonly IMapper _mapper;

        public UserService(
            IGenericRepository<User> repo,
            IGenericRepository<UserUnit> userUnitRepo,
            IGenericRepository<TaskItem> taskRepo,
            IGenericRepository<TaskAssignee> assigneeRepo,
            IGenericRepository<Progress> progressRepo,
            IMapper mapper)
        {
            _repo = repo;
            _userUnitRepo = userUnitRepo;
            _taskRepo = taskRepo;
            _assigneeRepo = assigneeRepo;
            _progressRepo = progressRepo;
            _mapper = mapper;
        }

        public async Task<List<UserDto>> GetAll()
            => _mapper.Map<List<UserDto>>(await _repo.Query()
                .Where(u => !u.IsDeleted && u.IsApproved)
                .ToListAsync());

        public async Task<Guid?> GetUnitIdAsync(Guid userId)
        {
            var user = await _repo.GetByIdAsync(userId);
            return user?.UnitId;
        }

        public async Task<bool> IsUserActive(Guid userId)
        {
            var user = await _repo.GetByIdAsync(userId);
            return user != null && user.IsApproved && !user.IsDeleted;
        }

        public async Task<List<UserDto>> GetByManager(Guid managerId)
        {
            var manager = await _repo.GetByIdAsync(managerId);
            if (manager?.UnitId == null) return new List<UserDto>();

            var unitId = manager.UnitId.Value;
            var userIdsFromMapping = await _userUnitRepo.Query()
                .Where(uu => uu.UnitId == unitId)
                .Select(uu => uu.UserId)
                .ToListAsync();

            var users = await _repo.Query()
                .Where(u => (u.UnitId == unitId || userIdsFromMapping.Contains(u.Id) || u.UnitId == null) // ✅ THÊM: Cho phép thấy cả người chưa gán phòng
                            && u.Role != "Admin"
                            && u.IsApproved
                            && !u.IsDeleted)
                .ToListAsync();

            return _mapper.Map<List<UserDto>>(users);
        }

        public async Task<List<UserDto>> Search(string keyword, string? role, Guid? unitId, Guid? managerId = null)
        {
            var query = _repo.Query().Where(u => u.Role != "Admin" && u.IsApproved && !u.IsDeleted);

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.Contains(keyword)) ||
                    (u.EmployeeCode != null && u.EmployeeCode.Contains(keyword)) ||
                    u.Username.Contains(keyword));

            if (!string.IsNullOrEmpty(role))
                query = query.Where(u => u.Role == role);

            if (managerId.HasValue)
            {
                // Manager tìm trong phòng mình HOẶC những người chưa có phòng
                var m = await _repo.GetByIdAsync(managerId.Value);
                if (m != null && m.UnitId.HasValue)
                {
                    var muId = m.UnitId.Value;
                    query = query.Where(u => u.UnitId == muId || u.UnitId == null);
                }
            }
            else if (unitId.HasValue)
            {
                query = query.Where(u => u.UnitId == unitId.Value);
            }

            return _mapper.Map<List<UserDto>>(await query.ToListAsync());
        }

        public async Task<UserDto> Update(Guid id, UpdateUserDto dto)
        {
            var user = await _repo.GetByIdAsync(id)
                ?? throw new Exception("User not found");

            var oldRole = user.Role;
            var isPromoting = oldRole != "Manager" && dto.Role == "Manager";
            var isDemoting = oldRole == "Manager" && dto.Role != "Manager";

            // 1. KIỂM TRA CHỐT CHẶN: Khi lên chức Trưởng phòng
            if (isPromoting)
            {
                var pendingTasks = await _taskRepo.Query()
                    .Where(t => !t.IsDeleted && t.Status != TaskStatusEnum.Approved)
                    .Join(_assigneeRepo.Query().Where(a => a.UserId == id), t => t.Id, a => a.TaskId, (t, a) => t)
                    .Select(t => t.Title)
                    .ToListAsync();

                if (pendingTasks.Any())
                {
                    throw new Exception($"Không thể nâng cấp lên Trưởng phòng. Nhân viên còn {pendingTasks.Count} việc chưa hoàn thành: {string.Join(", ", pendingTasks)}");
                }
            }

            // 2. XỬ LÝ LINH HOẠT TRƯỞNG PHÒNG CŨ (Bàn giao & Luân chuyển)
            if (dto.OldManagerId.HasValue && !string.IsNullOrEmpty(dto.OldManagerAction))
            {
                var oldM = await _repo.GetByIdAsync(dto.OldManagerId.Value);
                if (oldM != null && oldM.Role == "Manager")
                {
                    // Đã gỡ bỏ chốt chặn: Trưởng phòng cũ không cần phải duyệt hết toàn bộ việc của phòng mới được luân chuyển. 
                    // Công việc vẫn ở lại phòng và sẽ do Trưởng phòng mới (hoặc Admin) duyệt tiếp.

                    // Thực hiện hành động lựa chọn cho Trưởng phòng cũ
                    oldM.Role = "User"; // Mặc định về nhân viên
                    if (dto.OldManagerAction == "Demote")
                    {
                        // Giữ nguyên UnitId hiện tại
                    }
                    else if (dto.OldManagerAction == "Transfer")
                    {
                        if (oldM.UnitId != dto.OldManagerNewUnitId)
                        {
                            oldM.UnitId = dto.OldManagerNewUnitId;
                            oldM.JoinedUnitAt = DateTime.UtcNow; // ✅ Reset KPI cho manager cũ khi chuyển phòng
                        }
                    }
                    else if (dto.OldManagerAction == "Remove")
                    {
                        oldM.UnitId = null;
                        oldM.JoinedUnitAt = DateTime.UtcNow; // ✅ Reset KPI
                    }

                    _repo.Update(oldM);
                    
                    // Đồng bộ UserUnit cho người cũ
                    var oldMappingsOldM = await _userUnitRepo.Query().Where(uu => uu.UserId == oldM.Id).ToListAsync();
                    foreach (var m in oldMappingsOldM) _userUnitRepo.Delete(m);
                    if (oldM.UnitId.HasValue)
                    {
                        await _userUnitRepo.AddAsync(new UserUnit { Id = Guid.NewGuid(), UserId = oldM.Id, UnitId = oldM.UnitId.Value });
                    }
                }
            }

            // XÓA BỎ CHỐT CHẶN: Khi Trưởng phòng chủ động bị hạ cấp trực tiếp (không qua thay thế), 
            // KHÔNG YÊU CẦU phòng ban phải hoàn thành 100% công việc.

            // 4. KIỂM TRA CHỐT CHẶN: Khi thay đổi Phòng ban (Luân chuyển nhân viên)
            if (user.UnitId != dto.UnitId && user.UnitId.HasValue)
            {
                if (user.Role == "Manager")
                {
                    // XÓA BỎ CHỐT CHẶN: Khi Quản lý chuyển phòng, không yêu cầu phòng cũ phải trống việc.
                }
                else if (user.Role == "User")
                {
                    var pendingTasks = await _taskRepo.Query()
                        .Where(t => !t.IsDeleted && t.Status != TaskStatusEnum.Approved && t.UnitId == user.UnitId)
                        .Join(_assigneeRepo.Query().Where(a => a.UserId == id), t => t.Id, a => a.TaskId, (t, a) => t.Title)
                        .ToListAsync();

                    if (pendingTasks.Any())
                    {
                        throw new Exception($"Không thể thao tác! Nhân sự này đang đảm nhận {pendingTasks.Count} công việc chưa hoàn thành: {string.Join(", ", pendingTasks)}. Vui lòng vào trang Nhiệm vụ, bàn giao hoặc gỡ các công việc này trước khi luân chuyển.");
                    }
                }
            }

            // 5. Cập nhật thông tin chính cho User mục tiêu
            user.Role = dto.Role;
            if (user.UnitId != dto.UnitId)
            {
                user.UnitId = dto.UnitId;
                user.JoinedUnitAt = DateTime.UtcNow; // ✅ Reset KPI cho user khi chuyển phòng
            }

            _repo.Update(user);
            await _repo.SaveAsync();

            // 5. ĐỒNG BỘ: Cập nhật bảng liên kết UserUnit cho User mục tiêu
            var oldMappings = await _userUnitRepo.Query()
                .Where(uu => uu.UserId == id)
                .ToListAsync();
            foreach (var m in oldMappings) _userUnitRepo.Delete(m);

            if (user.UnitId.HasValue)
            {
                await _userUnitRepo.AddAsync(new UserUnit
                {
                    Id = Guid.NewGuid(),
                    UserId = id,
                    UnitId = user.UnitId.Value
                });
            }
            await _userUnitRepo.SaveAsync();

            return _mapper.Map<UserDto>(user);
        }

        public async Task Delete(Guid id)
        {
            var user = await _repo.GetByIdAsync(id)
                ?? throw new Exception("User not found");

            if (user.Role == "Admin")
                throw new Exception("Không thể xóa tài khoản Admin!");

            // ✅ CHỐT CHẶN: Kiểm tra công việc tồn đọng trước khi xóa
            // XÓA BỎ: Không chặn Manager xóa nếu phòng cũ còn việc.
            else if (user.Role == "User")
            {
                var pendingTasks = await _taskRepo.Query()
                    .Where(t => !t.IsDeleted && t.Status != TaskStatusEnum.Approved)
                    .Join(_assigneeRepo.Query().Where(a => a.UserId == id), t => t.Id, a => a.TaskId, (t, a) => t.Title)
                    .ToListAsync();

                if (pendingTasks.Any())
                {
                    throw new Exception($"Không thể thao tác! Nhân sự này đang đảm nhận {pendingTasks.Count} công việc: {string.Join(", ", pendingTasks)}. Vui lòng vào trang Nhiệm vụ, bàn giao công việc này cho người khác trước khi xóa.");
                }
            }

            user.IsDeleted = true;
            _repo.Update(user);
            
            // ✅ FIX: Xoá phân công nếu user bị xóa để tránh kẹt task
            var userAssignees = await _assigneeRepo.Query().Where(a => a.UserId == id).ToListAsync();
            foreach (var a in userAssignees) {
                _assigneeRepo.Delete(a);
            }

            await _repo.SaveAsync();
        }

        /// <summary>
        /// Lấy điểm KPI (Có phân luồng User thường vs Manager)
        /// </summary>
        public async Task<PerformanceDto> GetPerformanceAsync(Guid userId)
        {
            var user = await _repo.GetByIdAsync(userId)
                ?? throw new Exception("User not found");

            var now = DateTime.UtcNow;

            if (user.Role == "Manager")
                return await CalculateManagerPerformanceAsync(userId, user, now);

            return await CalculatePersonalPerformanceDtoAsync(userId, user, now, user.UnitId);
        }

        private async Task<PerformanceDto> CalculatePersonalPerformanceDtoAsync(Guid userId, User user, DateTime now, Guid? filterUnitId = null)
        {
            // 1. Task được giao
            var assignedTaskIds = await _assigneeRepo.Query()
                .Where(a => a.UserId == userId)
                .Select(a => a.TaskId)
                .ToListAsync();
 
            var tasks = await _taskRepo.Query()
                .Where(t => assignedTaskIds.Contains(t.Id) && !t.IsDeleted)
                .Where(t => !filterUnitId.HasValue || t.UnitId == filterUnitId.Value) 
                .ToListAsync();
 
            var taskIds = tasks.Select(t => t.Id).ToList();

            // 2. Task quá hạn = có deadline, chưa submit/approve, đã qua hạn
            var overdueTasks = tasks
                .Where(t => t.DueDate.HasValue
                         && t.DueDate.Value < now
                         && t.Status != TaskStatusEnum.Approved
                         && t.Status != TaskStatusEnum.Submitted)
                .ToList();

            // 3. Kiểm tra hoàn thành đúng/trễ hạn
            var approvedTasksWithDeadline = tasks
                .Where(t => t.Status == TaskStatusEnum.Approved && t.DueDate.HasValue)
                .ToList();

            // ✅ MỚI: Task Approved KHÔNG có deadline cũng được bonus nhỏ
            int approvedNoDeadlineCount = tasks
                .Count(t => t.Status == TaskStatusEnum.Approved && !t.DueDate.HasValue);

            var progressList = await _progressRepo.Query()
                .Where(p => p.UserId == userId && (taskIds.Contains(p.TaskId)))
                .ToListAsync();

            int completedOnTime = 0, completedLate = 0;
            foreach (var approvedTask in approvedTasksWithDeadline)
            {
                var lastProgress = progressList
                    .Where(p => p.TaskId == approvedTask.Id)
                    .OrderByDescending(p => p.UpdatedAt)
                    .FirstOrDefault();

                if (lastProgress != null && approvedTask.DueDate.HasValue)
                {
                    if (lastProgress.UpdatedAt <= approvedTask.DueDate.Value) completedOnTime++;
                    else completedLate++;
                }
            }

            // 4. Báo cáo bị từ chối
            int rejectedCount = progressList.Count(p => p.Status == TaskStatusEnum.Rejected);

            // 5. Tính điểm công bằng
            int bonusPoints = completedOnTime * 5;
            bonusPoints += approvedNoDeadlineCount * 3; // ✅ MỚI: +3 cho task không deadline được duyệt

            // ✅ MỚI: Streak bonus — thưởng chuỗi hoàn thành đúng hạn LIÊN TIẾP thực sự
            int currentStreak = 0;
            int maxStreak = 0;
            var orderedApprovedTasks = approvedTasksWithDeadline.OrderBy(t => t.DueDate).ToList();
            foreach (var approvedTask in orderedApprovedTasks)
            {
                var lastProgress = progressList
                    .Where(p => p.TaskId == approvedTask.Id)
                    .OrderByDescending(p => p.UpdatedAt)
                    .FirstOrDefault();

                if (lastProgress != null && approvedTask.DueDate.HasValue)
                {
                    if (lastProgress.UpdatedAt <= approvedTask.DueDate.Value) 
                    {
                        currentStreak++;
                        if (currentStreak > maxStreak) maxStreak = currentStreak;
                    }
                    else 
                    {
                        currentStreak = 0;
                    }
                }
            }

            if (maxStreak >= 5) bonusPoints += 5;       // 5+ liên tiếp: +5 bonus
            else if (maxStreak >= 3) bonusPoints += 2;  // 3-4 liên tiếp: +2 bonus

            int penaltyPoints = 0;

            // Phạt lũy tiến theo số lần vi phạm: lần 1=-5, lần 2=-8, lần 3+=-12
            for (int i = 0; i < overdueTasks.Count; i++)
                penaltyPoints += i == 0 ? 5 : i == 1 ? 8 : 12;

            penaltyPoints += rejectedCount * 3;
            int score = Math.Max(0, 100 + bonusPoints - penaltyPoints);

            // 6. Xác định cấp độ
            string level, levelColor, levelIcon;
            
            // ✅ SỬA: Nếu chưa có Task nào thì là Nhân viên mới (Reset về 100)
            if (tasks.Count == 0)
            {
                score = 100;
                level = "Mới/Thử việc"; levelColor = "gray"; levelIcon = "🐣";
            }
            else if (score >= 90)      { level = "Xuất sắc"; levelColor = "green";  levelIcon = "⭐"; }
            else if (score >= 75) { level = "Tốt";      levelColor = "blue";   levelIcon = "✅"; }
            else if (score >= 60) { level = "Trung bình"; levelColor = "yellow"; levelIcon = "⚠️"; }
            else                  { level = "Yếu";      levelColor = "red";    levelIcon = "🔴"; }

            // 7. Cảnh báo
            bool isAtRisk = overdueTasks.Count >= 3 || score < 60;
            string warning = "";
            if (overdueTasks.Count >= 3)
                warning = $"⚠️ Vi phạm {overdueTasks.Count} lần quá hạn! Cần cải thiện ngay.";
            else if (score < 60)
                warning = "⚠️ Điểm hiệu suất thấp! Cần chú ý cải thiện chất lượng công việc.";

            return new PerformanceDto
            {
                UserId = userId,
                FullName = user.FullName ?? "—",
                EmployeeCode = user.EmployeeCode ?? "—",
                Score = score,
                Level = level,
                LevelColor = levelColor,
                LevelIcon = levelIcon,
                TotalTasks = tasks.Count,
                CompletedOnTime = completedOnTime,
                CompletedLate = completedLate,
                OverdueTasks = overdueTasks.Count,
                RejectedReports = rejectedCount,
                BonusPoints = bonusPoints,
                PenaltyPoints = penaltyPoints,
                IsAtRisk = isAtRisk,
                WarningMessage = warning
            };
        }

        private async Task<PerformanceDto> CalculateManagerPerformanceAsync(Guid managerId, User user, DateTime now)
        {
            // 1. KPI Cá nhân (Các task mà Giám đốc giao trực tiếp cho Manager)
            var personalDto = await CalculatePersonalPerformanceDtoAsync(managerId, user, now, user.UnitId); // ✅ MỚI: Lọc theo UnitId của Manager
            int personalScore = personalDto.TotalTasks == 0 ? 100 : personalDto.Score;

            // 2. KPI Phòng ban (Bằng trung bình cộng nhân viên trong phòng)
            double unitAvgScore = 100;
            var unitPerformanceList = new List<PerformanceDto>();

            if (user.UnitId.HasValue)
            {
                var unitId = user.UnitId.Value;

                // ✅ SỬA: Chỉ lấy NV ĐANG thuộc phòng (theo User.UnitId trực tiếp)
                // NV đã chuyển đi sẽ không bị tính vào KPI Manager nữa
                var memberIds = await _repo.Query()
                    .Where(u => u.UnitId == unitId
                                && u.Role == "User"
                                && u.IsApproved
                                && !u.IsDeleted)
                    .Select(u => u.Id)
                    .ToListAsync();

                foreach (var uid in memberIds)
                {
                    var member = await _repo.GetByIdAsync(uid);
                    if (member != null)
                    {
                        // ✅ MỚI: Reset điểm tại phòng mới bằng cách lọc theo UnitId của Manager hiện tại
                        unitPerformanceList.Add(await CalculatePersonalPerformanceDtoAsync(uid, member, now, unitId));
                    }
                }
 
                if (unitPerformanceList.Count > 0)
                {
                    // ✅ SỬA: Chỉ tính trung bình cộng của những nhân viên THỰC SỰ có Task TẠI PHÒNG NÀY
                    var activeMembers = unitPerformanceList.Where(p => p.TotalTasks > 0).ToList();
                    if (activeMembers.Count > 0)
                        unitAvgScore = activeMembers.Average(p => p.Score);
                    else
                        unitAvgScore = 100; // Nếu toàn bộ là nhân viên mới chưa có task ở đây, mặc định 100 chờ đánh giá
                }
            }

            // 3. Phạt ngâm task (SLA Violation): Tiến độ Submitted nhưng Manager chưa duyệt quá 48h
            var memberIdsForReview = unitPerformanceList.Select(p => p.UserId).ToList();
            var pendingProgresses = await _progressRepo.Query()
                .Where(p => memberIdsForReview.Contains(p.UserId) && p.Status == TaskStatusEnum.Submitted)
                .ToListAsync();

            int reviewPenaltyCount = 0;
            foreach (var p in pendingProgresses)
            {
                var hoursSinceSubmitted = (now - p.UpdatedAt).TotalHours;
                var hoursSinceJoined = (now - user.JoinedUnitAt).TotalHours;

                // ✅ FIX: Chỉ phạt ngâm task (>48h) NẾU Trưởng phòng này đã tại vị ở phòng được ít nhất 48h.
                // Tránh việc sếp mới luân chuyển về gánh oan tội ngâm task của sếp cũ.
                if (hoursSinceSubmitted > 48 && hoursSinceJoined > 48)
                {
                    reviewPenaltyCount++;
                }
            }

            // ✅ SỬA: Giới hạn phạt SLA tối đa -15 điểm (tránh phạt quá nặng khi Manager nghỉ phép/ốm)
            int reviewPenaltyPoints = Math.Min(reviewPenaltyCount * 3, 15);

            // 4. Tổng kết điểm: 70% đóng góp phòng ban, 30% hoàn thành cá nhân, trừ điểm phạt quản lý
            int finalScore = (int)Math.Round(unitAvgScore * 0.7 + personalScore * 0.3) - reviewPenaltyPoints;
            finalScore = Math.Max(0, finalScore);

            string level, levelColor, levelIcon;
            if (finalScore >= 90)      { level = "Xuất sắc"; levelColor = "green";  levelIcon = "⭐"; }
            else if (finalScore >= 75) { level = "Tốt";      levelColor = "blue";   levelIcon = "✅"; }
            else if (finalScore >= 60) { level = "Trung bình"; levelColor = "yellow"; levelIcon = "⚠️"; }
            else                       { level = "Yếu";      levelColor = "red";    levelIcon = "🔴"; }

            bool isAtRisk = finalScore < 60 || reviewPenaltyCount > 0 || personalDto.IsAtRisk;
            List<string> warnings = new List<string>();
            
            if (reviewPenaltyCount > 0)
                warnings.Add($"⚠️ Nút thắt cổ chai: '{reviewPenaltyCount}' báo cáo bị ngâm chưa duyệt quá 48h!");
            
            if (finalScore < 60)
                warnings.Add("⚠️ Hiệu suất lãnh đạo phòng ban thấp, ảnh hưởng điểm KPI quản lý!");

            if (!string.IsNullOrEmpty(personalDto.WarningMessage))
                warnings.Add(personalDto.WarningMessage);

            string warning = string.Join(" | ", warnings);

            return new PerformanceDto
            {
                UserId = managerId,
                FullName = user.FullName ?? "—",
                EmployeeCode = user.EmployeeCode ?? "—",
                Score = finalScore,
                Level = level,
                LevelColor = levelColor,
                LevelIcon = levelIcon,
                TotalTasks = personalDto.TotalTasks,
                CompletedOnTime = personalDto.CompletedOnTime,
                CompletedLate = personalDto.CompletedLate,
                OverdueTasks = personalDto.OverdueTasks,
                RejectedReports = personalDto.RejectedReports,
                BonusPoints = personalDto.BonusPoints,
                PenaltyPoints = personalDto.PenaltyPoints,
                ReviewPenaltyPoints = reviewPenaltyPoints,
                IsManagerKpi = true,
                UnitAverageScore = unitAvgScore,
                PersonalScore = personalScore,
                IsAtRisk = isAtRisk,
                WarningMessage = warning
            };
        }

        /// <summary>
        /// Lấy bảng KPI toàn phòng cho Manager — sắp xếp theo điểm giảm dần
        /// </summary>
        public async Task<List<PerformanceDto>> GetUnitPerformanceAsync(Guid managerId)
        {
            var manager = await _repo.GetByIdAsync(managerId);
            if (manager?.UnitId == null) return new List<PerformanceDto>();

            var unitId = manager.UnitId.Value;

            // ✅ SỬA: Chỉ lấy NV ĐANG thuộc phòng (theo User.UnitId trực tiếp)
            var memberIds = await _repo.Query()
                .Where(u => u.UnitId == unitId
                            && u.Role == "User"
                            && u.IsApproved
                            && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync();

            var result = new List<PerformanceDto>();
            foreach (var uid in memberIds)
            {
                var member = await _repo.GetByIdAsync(uid);
                if (member != null)
                {
                    // ✅ SỬA LỖI: Truyền unitId để lọc đúng bảng vàng cho phòng hiện tại
                    result.Add(await CalculatePersonalPerformanceDtoAsync(uid, member, DateTime.UtcNow, unitId));
                }
            }

            return result.OrderByDescending(p => p.Score).ToList();
        }
    }
}
