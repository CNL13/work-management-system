using System;

namespace WorkManagementSystem.Application.DTOs
{
    public class CommentDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        
        // Trả về thêm để hiển thị trên UI
        public string? UserFullName { get; set; }
        public string? UserEmployeeCode { get; set; }

        public List<ReactionSummaryDto> Reactions { get; set; } = new();
        public List<string> SeenByUserFullNames { get; set; } = new(); // ✅ MỚI
        public string? MyReaction { get; set; }
    }

    public class ReactionSummaryDto
    {
        public string Emoji { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<string> UserFullNames { get; set; } = new();
    }

    public class CreateCommentDto
    {
        public Guid TaskId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
