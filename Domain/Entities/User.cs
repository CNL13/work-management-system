namespace WorkManagementSystem.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }

        public string Username { get; set; }
        public string PasswordHash { get; set; }

        public string Role { get; set; } // "User" | "Manager"

        public ICollection<UserUnit> UserUnits { get; set; } = new List<UserUnit>();
    }
}