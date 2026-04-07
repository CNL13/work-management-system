namespace WorkManagementSystem.Application.DTOs
{
    public class CreateProgressDto
    {
        public Guid TaskId { get; set; }
        public int Percent { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    // ✅ Thêm class này vào
    public class ProgressDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public int Percent { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}