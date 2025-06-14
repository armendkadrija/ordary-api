using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Services;
using Odary.Api.Domain;

namespace Odary.Api.Infrastructure.Database;

public class OdaryDbContext : IdentityDbContext<User, Role, string>
{
    private readonly IAuditService? _auditService;

    public OdaryDbContext(DbContextOptions<OdaryDbContext> options) : base(options)
    {
    }

    public OdaryDbContext(DbContextOptions<OdaryDbContext> options, IAuditService auditService) : base(options)
    {
        _auditService = auditService;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();

        // Create audit logs before saving
        List<AuditLog>? auditLogs = null;
        if (_auditService != null)
        {
            var auditableEntries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted)
                .ToList();

            auditLogs = await _auditService.CreateAuditLogsAsync(auditableEntries);
        }

        // Save the main changes
        var result = await base.SaveChangesAsync(cancellationToken);

        // Save audit logs in a separate transaction to avoid recursion
        if (auditLogs != null && auditLogs.Any())
        {
            AuditLogs.AddRange(auditLogs);
            await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();

        // Create audit logs before saving
        List<AuditLog>? auditLogs = null;
        if (_auditService != null)
        {
            var auditableEntries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted)
                .ToList();

            auditLogs = _auditService.CreateAuditLogsAsync(auditableEntries).GetAwaiter().GetResult();
        }

        // Save the main changes
        var result = base.SaveChanges();

        // Save audit logs in a separate transaction to avoid recursion
        if (auditLogs != null && auditLogs.Any())
        {
            AuditLogs.AddRange(auditLogs);
            base.SaveChanges();
        }

        return result;
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

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantSettings> TenantSettings { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Patient> Patients { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ASP.NET Identity table names to use clean snake_case
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Role>().ToTable("roles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("user_roles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("user_claims");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("user_logins");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("role_claims");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("user_tokens");

        // Configure complex relationships and database-specific concerns
        modelBuilder.Entity<User>(entity =>
        {
            // Indexes (can't be done with annotations in a clean way)
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.LastLoginAt);

            // Foreign key relationship with Tenant (nullable for BusinessAdmin)
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(e => e.TenantId)
                .IsRequired(false) // Allow null for BusinessAdmin users
                .OnDelete(DeleteBehavior.Restrict);

            // Password history: List<string> automatically maps to text[] in PostgreSQL with Npgsql
            // No explicit configuration needed - EF Core conventions handle this perfectly!
        });

        modelBuilder.Entity<Role>(entity =>
        {
            // Roles are now global - ensure unique role names
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Slug).IsUnique();
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

        modelBuilder.Entity<AuditLog>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });

            // Foreign key relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);

            // Foreign key relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.PhoneNumber);
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => new { e.FirstName, e.LastName });
            entity.HasIndex(e => e.IsArchived);

            // Foreign key relationship with Tenant
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // PostgreSQL natively supports List<string> as text[] arrays
            // No explicit configuration needed - EF Core + Npgsql handle this automatically!
        });
    }
}