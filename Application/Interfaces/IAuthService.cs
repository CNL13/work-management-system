using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUploadService
    {
        Task<UploadFileDto> UploadAsync(IFormFile file, Guid? progressId, Guid? taskId);
        Task<UploadFileDto?> GetFileByIdAsync(Guid id);      // ✅ MỚI
    }

    public interface IAuthService
    {
        Task<string> Register(AuthDto dto);
        Task<string> Login(string username, string password);
        Task<string> ResetPassword(ResetPasswordDto dto);
        Task<string> ApproveUser(Guid userId);
        Task<string> RejectUser(Guid userId);
        Task<List<UserDto>> GetPendingUsers();
        Task<string> RefreshToken(Guid userId);
    }

    public interface ITaskService
    {
        Task<TaskDto> Create(CreateTaskDto dto, Guid userId);
        Task<object> Get(string keyword, int page, int size, string? status, Guid currentUserId, Guid? userId = null, Guid? unitId = null);
        Task<TaskDto> GetByIdAsync(Guid id, Guid userId);
        Task<List<TaskHistory>> GetHistoryAsync(Guid taskId, Guid userId);
        Task<TaskDto> Update(Guid id, CreateTaskDto dto, Guid changedBy);  // ✅ SỬA: thêm changedBy cho Audit Log
        Task UpdateStatus(Guid id, string status, Guid userId); // ✅ MỚI
        Task Delete(Guid id, Guid userId); // ✅ SỬA: Bảo mật ai được quyền xóa
        Task<Guid?> GetManagerUnitId(Guid managerId);
        Task RemindTask(Guid taskId, Guid reminderId);
        Task Reorder(Guid id, int newIndex, Guid userId); // ✅ MỚI: Kéo thả Kanban
    }

    public interface IProgressService
    {
        Task<ProgressDto> Update(CreateProgressDto dto);
        Task<object> GetAll(int page, int size, Guid? userId = null, Guid? unitId = null);  // ✅ SỬA: thêm unitId
        Task<List<ProgressDto>> GetByTaskAsync(Guid taskId, Guid userId); // ✅ MỚI
        Task<object> GetMyHistory(Guid userId, int page, int size); // ✅ MỚI: Lịch sử cá nhân toàn bộ
    }

    public interface IReviewService
    {
        Task<ReviewDto> Review(ReviewDto dto, Guid reviewerId);  // ✅ SỬA: thêm reviewerId
    }

    public interface IUnitService
    {
        Task<List<UnitDto>> GetAll();
        Task<UnitDto?> GetMyUnit(Guid userId);
        Task<List<UserDto>> GetUsers(Guid unitId);
        Task<UnitDto> Create(CreateUnitDto dto);
        Task<UnitDto> Update(Guid id, CreateUnitDto dto);
        Task Delete(Guid id);
        Task AddMember(Guid unitId, Guid userId);
        Task RemoveMember(Guid unitId, Guid userId);
    }

    public interface IUserService
    {
        Task<List<UserDto>> GetAll();
        Task<List<UserDto>> GetByManager(Guid managerId);
        Task<List<UserDto>> Search(string keyword, string? role, Guid? unitId, Guid? managerId = null);
        Task<UserDto> Update(Guid id, UpdateUserDto dto);
        Task Delete(Guid id);
        Task<PerformanceDto> GetPerformanceAsync(Guid userId);          // ✅ MỚI: KPI cá nhân
        Task<List<PerformanceDto>> GetUnitPerformanceAsync(Guid managerId); // ✅ MỚI: KPI toàn phòng
        Task<Guid?> GetUnitIdAsync(Guid userId); // ✅ MỚI
        Task<bool> IsUserActive(Guid userId);
    }

    public interface INotificationService
    {
        Task AddNotification(Guid userId, string message);
        Task<List<NotificationDto>> GetMyNotifications(Guid userId);
        Task MarkAsRead(Guid notificationId);
        Task<int> GetUnreadCount(Guid userId);
    }

    public interface IExportService
    {
        Task<byte[]> ExportTasksToExcel();
        Task<byte[]> ExportProgressToExcel();
    }

    public interface IChangePasswordService
    {
        Task<string> ChangePassword(Guid userId, ChangePasswordDto dto);
    }

    public interface IProfileService
    {
        Task<ProfileDto?> GetProfile(Guid userId);
        Task<string> UpdateProfile(Guid userId, ProfileDto dto);
    }

    public interface IDashboardService
    {
        Task<DashboardDto> GetDashboard();
        Task<ManagerDashboardDto> GetManagerDashboard(Guid userId);
    }

    public interface ICommentService
    {
        Task<CommentDto> AddComment(CreateCommentDto dto, Guid userId);
        Task<List<CommentDto>> GetComments(Guid taskId, Guid? userId = null); // ✅ SỬA: Thêm userId để lấy MyReaction
        Task Delete(Guid commentId, Guid userId); // Chỉ người gửi mới được xóa
        Task<Guid> ToggleReaction(Guid commentId, Guid userId, string emoji); // ✅ SỬA: Trả về taskId để broadcast hub
        Task MarkAsSeen(Guid taskId, Guid userId); // ✅ MỚI
    }

    public interface ISubTaskService
    {
        Task<SubTaskDto> AddSubTask(CreateSubTaskDto dto, Guid userId);
        Task ToggleSubTask(Guid id, Guid userId);
        Task Delete(Guid id, Guid userId);
        Task<List<SubTaskDto>> GetSubTasks(Guid taskId);
    }
}
