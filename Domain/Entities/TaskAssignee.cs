namespace WorkManagementSystem.Domain.Entities
{
    public class TaskAssignee
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? UnitId { get; set; }
    }
}
