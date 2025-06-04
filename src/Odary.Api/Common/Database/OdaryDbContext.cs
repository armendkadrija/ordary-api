using Microsoft.EntityFrameworkCore;
using Odary.Api.Domain;

namespace Odary.Api.Common.Database;

public class OdaryDbContext : DbContext
{
    public OdaryDbContext(DbContextOptions<OdaryDbContext> options) : base(options)
    {
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
                    break;
            }
        }
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantSettings> TenantSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure complex relationships and database-specific concerns
        modelBuilder.Entity<User>(entity =>
        {
            // Indexes (can't be done with annotations in a clean way)
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.TenantId);

            // Foreign key relationship with Tenant
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<TenantSettings>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.TenantId).IsUnique();

            // Foreign key relationship with Tenant (one-to-one)
            entity.HasOne(e => e.Tenant)
                .WithOne(t => t.Settings)
                .HasForeignKey<TenantSettings>(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }


} 