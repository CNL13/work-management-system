namespace WorkManagementSystem.Application.DTOs
{
    public class AuthDto
    {
        public string Username { get; set; }
        public string Password { get; set; }

        // optional
        public string? Role { get; set; }
    }
}