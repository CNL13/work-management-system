using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Ý NGHĨA: Hàm lấy dữ liệu thống kê tổng quát dành cho Admin.
        /// - Tổng hợp số lượng Task, User, Unit trên toàn hệ thống.
        /// - Thống kê trạng thái công việc chi tiết dựa trên bảng Task.
        /// - Tóm tắt hiệu suất làm việc theo từng đơn vị (Unit).
        /// </summary>
        public async Task<DashboardDto> GetDashboard()
        {
            var tasks = await _context.Tasks.ToListAsync();
            var units = await _context.Units.ToListAsync();

            return new DashboardDto
            {
                TotalTasks = tasks.Count,
                TotalUsers = await _context.Users.CountAsync(),
                TotalUnits = units.Count,

                // Đếm số lượng công việc theo từng trạng thái cụ thể
                TaskPending = tasks.Count(t => t.Status == TaskStatus.NotStarted),
                TaskInProgress = tasks.Count(t => t.Status == TaskStatus.InProgress),
                TaskApproved = tasks.Count(t => t.Status == TaskStatus.Approved),
                TaskRejected = tasks.Count(t => t.Status == TaskStatus.Rejected),
                ReportSubmitted = tasks.Count(t => t.Status == TaskStatus.Submitted),

                // Tạo danh sách tóm tắt cho từng phòng ban (Unit)
                UnitSummaries = units.Select((u, index) => new UnitSummaryDto
                {
                    UnitName = u.Name,
                    UnitCode = $"UNIT-{(index + 1):D2}",
                    // Tính tổng số Task được giao cho đơn vị này thông qua bảng Assignees
                    TotalTasks = _context.TaskAssignees
                        .Count(ta => ta.UnitId == u.Id),
                    // Đếm số lượng Task đã hoàn thành và được phê duyệt thuộc đơn vị này
                    ApprovedTasks = _context.TaskAssignees
                        .Count(ta => ta.UnitId == u.Id &&
                            _context.Tasks.Any(t => t.Id == ta.TaskId &&
                                t.Status == TaskStatus.Approved))
                }).ToList()
            };
        }

        /// <summary>
        /// Ý NGHĨA: Hàm lấy dữ liệu thống kê dành riêng cho Manager của một đơn vị.
        /// - Xác định phòng ban mà Manager phụ trách.
        /// - Thống kê danh sách nhân viên và các công việc liên quan đến phòng ban đó.
        /// - Tính toán tiến độ làm việc chi tiết của từng thành viên trong phòng.
        /// </summary>
        public async Task<ManagerDashboardDto> GetManagerDashboard(Guid userId)
        {
            // Bước 1: Xác định phòng ban (Unit) của người quản lý
            var userUnit = await _context.UserUnits
                .FirstOrDefaultAsync(uu => uu.UserId == userId);

            if (userUnit == null)
                return new ManagerDashboardDto { UnitName = "Chưa có phòng ban" };

            var unitId = userUnit.UnitId;
            var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == unitId);

            // Bước 2: Lấy danh sách thành viên thuộc quyền quản lý của Unit này
            var members = await _context.Users
                .Where(u => u.UnitId == unitId && u.Role != "Manager" && u.IsApproved && !u.IsDeleted)
                .ToListAsync();

            var memberIds = members.Select(m => m.Id).ToList();

            // Bước 3: Thu thập ID của tất cả các công việc liên quan đến Unit hoặc thành viên trong Unit
            var taskIds = await _context.TaskAssignees
                .Where(ta => ta.UnitId == unitId || (ta.UserId.HasValue && memberIds.Contains(ta.UserId.Value)))
                .Select(ta => ta.TaskId)
                .Distinct()
                .ToListAsync();

            // Lấy thông tin chi tiết các Task để thực hiện đếm trạng thái
            var tasks = await _context.Tasks
                .Where(t => taskIds.Contains(t.Id))
                .ToListAsync();

            // Lấy danh sách phân công để phục vụ việc tính toán tiến độ từng cá nhân
            var assignees = await _context.TaskAssignees
                .Where(a => a.UserId.HasValue && memberIds.Contains(a.UserId.Value))
                .ToListAsync();

            // Bước 4: Thống kê hiệu suất chi tiết cho từng thành viên (MemberProgresses)
            var memberProgresses = members.Select(m =>
            {
                // Lọc các Task mà nhân viên cụ thể này tham gia
                var myTaskIds = assignees.Where(a => a.UserId == m.Id).Select(a => a.TaskId).Distinct().ToList();
                var myTasks = tasks.Where(t => myTaskIds.Contains(t.Id)).ToList();

                return new MemberProgressDto
                {
                    FullName = m.FullName,
                    UserEmployeeCode = m.EmployeeCode,
                    TotalTasks = myTasks.Count,
                    ApprovedTasks = myTasks.Count(t => t.Status == TaskStatus.Approved),
                    SubmittedTasks = myTasks.Count(t => t.Status == TaskStatus.Submitted)
                };
            }).ToList();

            // Bước 5: Trả về kết quả tổng hợp cho Dashboard của Manager
            return new ManagerDashboardDto
            {
                UnitName = unit?.Name ?? "",
                TotalMembers = members.Count,
                TotalTasks = taskIds.Count,

                // Thống kê trạng thái các công việc trong phạm vi quản lý của phòng ban
                TaskPending = tasks.Count(t => t.Status == TaskStatus.NotStarted),
                TaskInProgress = tasks.Count(t => t.Status == TaskStatus.InProgress),
                TaskApproved = tasks.Count(t => t.Status == TaskStatus.Approved),
                TaskRejected = tasks.Count(t => t.Status == TaskStatus.Rejected),
                ReportSubmitted = tasks.Count(t => t.Status == TaskStatus.Submitted),

                MemberProgresses = memberProgresses
            };
        }
    }
}