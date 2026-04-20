namespace WorkManagementSystem.Application.DTOs
{
    public class SubTaskDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateSubTaskDto
    {
        public Guid TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}
