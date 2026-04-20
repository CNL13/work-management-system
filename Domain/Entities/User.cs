namespace WorkManagementSystem.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string PasswordHash { get; set; }
        public string Role { get; set; } = "User"; // Admin, Manager, User
        public Guid? UnitId { get; set; }
        public DateTime JoinedUnitAt { get; set; } = DateTime.UtcNow; // ✅ MỚI: Reset KPI khi đổi phòng ban
        public bool IsApproved { get; set; } = false;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsDeleted { get; set; } = false;  // ✅ MỚI: Soft delete
        public ICollection<UserUnit> UserUnits { get; set; } = new List<UserUnit>();
    }
}
