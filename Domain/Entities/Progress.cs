using WorkManagementSystem.Domain.Enums;
using TaskStatus = WorkManagementSystem.Domain.Enums.TaskStatus; // ← Thêm dòng này

namespace WorkManagementSystem.Domain.Entities
{
    public class Progress
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public int Percent { get; set; }
        public string Description { get; set; }
        public TaskStatus Status { get; set; }  // ← Dòng 8 sẽ hết lỗi
    }
}