namespace WorkManagementSystem.Application.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;      // ✅ thêm
        public string EmployeeCode { get; set; } = string.Empty;  // ✅ thêm
        public string Role { get; set; } = string.Empty;
        public Guid? UnitId { get; set; }
        public bool IsApproved { get; set; }                      // ✅ thêm
        public string? PhoneNumber { get; set; }
    }

    public class UpdateUserDto
    {
        public string Role { get; set; } = string.Empty;
        public Guid? UnitId { get; set; }

        // ✅ MỚI: Hỗ trợ linh hoạt bàn giao/luân chuyển
        public Guid? OldManagerId { get; set; }
        public string? OldManagerAction { get; set; } // "Demote", "Transfer", "Remove"
        public Guid? OldManagerNewUnitId { get; set; }
    }
}