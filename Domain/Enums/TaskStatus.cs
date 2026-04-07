
namespace WorkManagementSystem.Domain.Enums  // ✅ Phải có "System"
{
    public enum TaskStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Submitted = 2,
        Approved = 3,
        Rejected = 4
    }
}