namespace WorkManagementSystem.Application.DTOs
{
    public class ReviewDto
    {
        public Guid ProgressId { get; set; }
        public bool Approve { get; set; }
        public string Comment { get; set; }
    }
}
