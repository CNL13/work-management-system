using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface IUploadService
    {
        Task<UploadFile> UploadAsync(IFormFile file, Guid? progressId);
    }
    public interface IAuthService
    {
        Task<string> Register(AuthDto dto);
        Task<string> Login(string username, string password);
    }

    public interface ITaskService
    {
        Task<TaskDto> Create(CreateTaskDto dto, Guid userId);
        Task<object> Get(string keyword, int page, int size, string? status);  // ✅ thêm status
        Task<TaskDto> Update(Guid id, CreateTaskDto dto);                       // ✅ thêm
        Task Delete(Guid id);                                                   // ✅ thêm
    }

    public interface IProgressService
    {
        Task<ProgressDto> Update(CreateProgressDto dto);
        Task<object> GetAll(int page, int size);                                // ✅ thêm
    }

    public interface IReviewService
    {
        Task<ReviewDto> Review(ReviewDto dto);
    }

    public interface IUnitService                                               // ✅ thêm mới
    {
        Task<List<UnitDto>> GetAll();
        Task<UnitDto> Create(CreateUnitDto dto);
        Task<UnitDto> Update(Guid id, CreateUnitDto dto);
        Task Delete(Guid id);
    }

    public interface IUserService                                               // ✅ thêm mới
    {
        Task<List<UserDto>> GetAll();
        Task<UserDto> Update(Guid id, UpdateUserDto dto);
        Task Delete(Guid id);
    }
}