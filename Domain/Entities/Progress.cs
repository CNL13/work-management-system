using WorkManagementSystem.Domain.Enums;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Domain.Entities
{
    public class Progress
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }                                    // ✅ thêm
        public int Percent { get; set; }
        public string Description { get; set; } = string.Empty;
        public TaskStatus Status { get; set; }
        public decimal HoursSpent { get; set; } = 0; // ✅ MỚI
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;         // ✅ thêm

        // Navigation Properties
        public TaskItem? Task { get; set; }
        public User? User { get; set; }
    }
}