using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Take_Leave.Models;

namespace Take_Leave.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Department> Departments { get; set; }
        public DbSet<Take_Leave.Models.Notice> Notices { get; set; }


        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<NotificationLog> NotificationLogs { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Department - HeadUser relationship with NO CASCADE
            builder.Entity<Department>()
                .HasOne(d => d.HeadUser)
                .WithMany()
                .HasForeignKey(d => d.HeadUserId)
                .OnDelete(DeleteBehavior.Restrict); // Changed to Restrict

            // Other relationships
            builder.Entity<LeaveRequest>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<LeaveBalance>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<NotificationLog>()
                .HasOne(n => n.LeaveRequest)
                .WithMany()
                .HasForeignKey(n => n.LeaveRequestId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<NotificationLog>()
                .HasOne(n => n.RecipientUser)
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ActivityLog relationships — all optional, since a general activity
            // event (e.g. "Staff registered", "Department added") may not be
            // tied to a specific leave request, recipient, or performing user.
            builder.Entity<ActivityLog>()
                .HasOne(a => a.LeaveRequest)
                .WithMany()
                .HasForeignKey(a => a.LeaveRequestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ActivityLog>()
                .HasOne(a => a.RecipientUser)
                .WithMany()
                .HasForeignKey(a => a.RecipientUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ActivityLog>()
                .HasOne(a => a.PerformedByUser)
                .WithMany()
                .HasForeignKey(a => a.PerformedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Notice <-> Department many-to-many.
            // Unidirectional: Department has no collection back to Notices, so we
            // configure it explicitly here. EF Core auto-creates the join table
            // "DepartmentNotice" with two FK columns.
            builder.Entity<Notice>()
                .HasMany(n => n.Departments)
                .WithMany();
        }
    }
}
