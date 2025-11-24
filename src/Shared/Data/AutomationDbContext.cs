using Microsoft.EntityFrameworkCore;
using SecureHumanLoopCaptcha.Shared.Entities;

namespace SecureHumanLoopCaptcha.Shared.Data;

public class AutomationDbContext : DbContext
{
    public AutomationDbContext(DbContextOptions<AutomationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AutomationRecord> Records => Set<AutomationRecord>();

    public DbSet<RecordAction> Actions => Set<RecordAction>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AutomationRecord>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedUtc = utcNow;
            }

            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedUtc = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<RecordAction>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedUtc = utcNow;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AutomationRecord>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Status)
                .HasConversion<int>();
            entity.Property(r => r.EncryptedPayload)
                .IsRequired();
            entity.Property(r => r.Source)
                .HasMaxLength(128);
            entity.HasMany(r => r.Actions)
                .WithOne(a => a.Record!)
                .HasForeignKey(a => a.RecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecordAction>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Actor)
                .HasMaxLength(128);
            entity.Property(a => a.ActionType)
                .HasMaxLength(64);
        });
    }
}
