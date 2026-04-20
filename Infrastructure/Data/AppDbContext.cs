using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<UserUnit> UserUnits { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<TaskAssignee> TaskAssignees { get; set; }
        public DbSet<Progress> Progresses { get; set; }
        public DbSet<UploadFile> UploadFiles { get; set; }
        public DbSet<ReportReview> Reviews { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<TaskHistory> TaskHistories { get; set; }  // ✅ MỚI: Audit log
        public DbSet<TaskComment> TaskComments { get; set; }  // ✅ MỚI: Thảo luận công việc
        public DbSet<CommentReaction> CommentReactions { get; set; } // ✅ MỚI: Cảm xúc tin nhắn
        public DbSet<CommentSeen> CommentSeens { get; set; }      // ✅ MỚI: Trạng thái đã xem tin nhắn
        public DbSet<SubTask> SubTasks { get; set; }           // ✅ MỚI: Công việc con (Checklist)

        // ✅ MỚI: Global Query Filter — tự động lọc bản ghi đã xóa mềm
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<TaskItem>().HasQueryFilter(t => !t.IsDeleted);
            modelBuilder.Entity<Unit>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<TaskComment>().HasQueryFilter(c => !c.IsDeleted); 

            // Cấu hình độ chính xác cho Decimal (Tránh cảnh báo)
            modelBuilder.Entity<TaskItem>()
                .Property(t => t.EstimatedHours).HasPrecision(18, 2);
            modelBuilder.Entity<TaskItem>()
                .Property(t => t.ActualHours).HasPrecision(18, 2);
            modelBuilder.Entity<Progress>()
                .Property(p => p.HoursSpent).HasPrecision(18, 2);

            // ✅ MỚI: Data Duplication Prevention (Unique Constraints)
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.EmployeeCode).IsUnique();
            modelBuilder.Entity<Unit>().HasIndex(u => u.Name).IsUnique();
            modelBuilder.Entity<UserUnit>().HasIndex(uu => new { uu.UserId, uu.UnitId }).IsUnique();
            modelBuilder.Entity<TaskAssignee>().HasIndex(ta => new { ta.TaskId, ta.UserId }).IsUnique();
            modelBuilder.Entity<SubTask>().HasIndex(st => new { st.TaskId, st.Title }).IsUnique();

            // ✅ MỚI: Foreign Key Configuration
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Creator)
                .WithMany()
                .HasForeignKey(t => t.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Unit)
                .WithMany()
                .HasForeignKey(t => t.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Progress>()
                .HasOne(p => p.Task)
                .WithMany()
                .HasForeignKey(p => p.TaskId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Progress>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskAssignee>()
                .HasOne(ta => ta.Task)
                .WithMany()
                .HasForeignKey(ta => ta.TaskId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TaskAssignee>()
                .HasOne(ta => ta.User)
                .WithMany()
                .HasForeignKey(ta => ta.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TaskAssignee>()
                .HasOne(ta => ta.Unit)
                .WithMany()
                .HasForeignKey(ta => ta.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReportReview>()
                .HasOne(r => r.Reviewer)
                .WithMany()
                .HasForeignKey(r => r.ReviewerId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ReportReview>()
                .HasOne(r => r.Progress)
                .WithMany()
                .HasForeignKey(r => r.ProgressId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
