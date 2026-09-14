using Enrichly.JobAutomation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrichly.JobAutomation.Infrastructure.Persistence;

public sealed class JobAutomationDbContext(DbContextOptions<JobAutomationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("Jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TargetUrl).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            entity.Property(x => x.RequestBody).HasMaxLength(10_000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.IsHourly, x.NextScheduledAt });
            entity.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            entity.HasOne(x => x.Owner).WithMany(x => x.Jobs).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobExecution>(entity =>
        {
            entity.ToTable("JobExecutions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClaimedByWorkerId).HasMaxLength(100);
            entity.Property(x => x.ErrorMessage).HasMaxLength(4_000);
            entity.Property(x => x.ResponseBodyPreview).HasMaxLength(4_000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
            entity.HasIndex(x => new { x.JobId, x.QueuedAt });
            entity.HasIndex(x => new { x.Status, x.ClaimedAt });
            entity.HasOne(x => x.Job).WithMany(x => x.Executions).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
