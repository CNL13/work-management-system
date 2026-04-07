namespace WorkManagementSystem.Application.DTOs
{
    public class UnitDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CreateUnitDto
    {
        public string Name { get; set; } = string.Empty;
    }
}