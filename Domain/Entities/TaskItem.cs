using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Domain.Entities
{
    public class TaskItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public TaskStatusEnum Status { get; set; } = TaskStatusEnum.NotStarted;
        public int OrderIndex { get; set; } = 0; // ✅ MỚI: Hỗ trợ kéo thả Kanban
        public decimal EstimatedHours { get; set; } = 0; // ✅ MỚI: Dự kiến
        public decimal ActualHours { get; set; } = 0;    // ✅ MỚI: Thực tế
        public Guid? UnitId { get; set; }        // ✅ MỚI: Thuộc về phòng ban nào
        public bool IsDeleted { get; set; } = false;  // ✅ MỚI: Soft delete
        
        // Navigation Properties
        public User? Creator { get; set; }
        public Unit? Unit { get; set; }
    }
}
