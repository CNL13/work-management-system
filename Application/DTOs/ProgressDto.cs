using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs
{
    public class CreateProgressDto
    {
        [Required]
        public Guid TaskId { get; set; }

        public Guid UserId { get; set; }

        [Range(0, 100, ErrorMessage = "Phần trăm hoàn thành phải từ 0 đến 100!")]
        public int Percent { get; set; }

        [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự!")]
        public string Description { get; set; } = string.Empty;

        public Guid? FileId { get; set; } // ✅ Gắn file khi nộp báo cáo
        public decimal HoursSpent { get; set; } = 0; // ✅ MỚI
    }

    public class ProgressDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty; // ✅ Tên task
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmployeeCode { get; set; } = string.Empty;
        public int Percent { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public List<UploadFileDto> Files { get; set; } = new();
        public decimal HoursSpent { get; set; } // ✅ MỚI
        public string? ReviewComment { get; set; } // ✅ MỚI: Nhận xét chuyên môn từ sếp
        public string? UnitName { get; set; } // ✅ MỚI: Tên phòng ban (cho lịch sử cá nhân)
    }
}
