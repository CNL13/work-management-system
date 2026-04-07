namespace WorkManagementSystem.Domain.Entities
{
    public class ReportReview
    {
        public Guid Id { get; set; }
        public Guid ProgressId { get; set; }
        public bool IsApproved { get; set; }
        public string? Comment { get; set; }
        public DateTime ReviewedAt { get; set; }
    }
}