using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.Entities;

namespace SalesCRM.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Zone> Zones => Set<Zone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(10);
            e.Property(u => u.Name).HasMaxLength(100);
            e.Property(u => u.Email).HasMaxLength(150);
            e.HasOne(u => u.Zone).WithMany(z => z.Users).HasForeignKey(u => u.ZoneId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(u => u.Region).WithMany(r => r.Users).HasForeignKey(u => u.RegionId).OnDelete(DeleteBehavior.SetNull);
        });

        // Region
        modelBuilder.Entity<Region>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(100);
            e.HasIndex(r => r.Name).IsUnique();
        });

        // Zone
        modelBuilder.Entity<Zone>(e =>
        {
            e.Property(z => z.Name).HasMaxLength(100);
            e.HasIndex(z => z.Name).IsUnique();
            e.HasOne(z => z.Region).WithMany(r => r.Zones).HasForeignKey(z => z.RegionId).OnDelete(DeleteBehavior.Cascade);
        });

        // Lead
        modelBuilder.Entity<Lead>(e =>
        {
            e.Property(l => l.School).HasMaxLength(200);
            e.Property(l => l.Board).HasMaxLength(50);
            e.Property(l => l.City).HasMaxLength(100);
            e.Property(l => l.State).HasMaxLength(100);
            e.Property(l => l.Type).HasMaxLength(50);
            e.Property(l => l.Source).HasMaxLength(50);
            e.Property(l => l.Stage).HasConversion<string>().HasMaxLength(30);
            e.Property(l => l.Value).HasColumnType("decimal(18,2)");
            e.Property(l => l.ContactName).HasMaxLength(150);
            e.Property(l => l.ContactDesignation).HasMaxLength(100);
            e.Property(l => l.ContactPhone).HasMaxLength(30);
            e.Property(l => l.ContactEmail).HasMaxLength(150);
            e.HasIndex(l => new { l.School, l.City });
            e.HasOne(l => l.Fo).WithMany(u => u.Leads).HasForeignKey(l => l.FoId).OnDelete(DeleteBehavior.Restrict);
        });

        // Activity
        modelBuilder.Entity<Activity>(e =>
        {
            e.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(20);
            e.HasOne(a => a.Fo).WithMany(u => u.Activities).HasForeignKey(a => a.FoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Lead).WithMany(l => l.Activities).HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.FoId, a.Date });
        });

        // Deal
        modelBuilder.Entity<Deal>(e =>
        {
            e.Property(d => d.ContractValue).HasColumnType("decimal(18,2)");
            e.Property(d => d.Discount).HasColumnType("decimal(5,2)");
            e.Property(d => d.FinalValue).HasColumnType("decimal(18,2)");
            e.Property(d => d.PaymentTerms).HasMaxLength(200);
            e.Property(d => d.Duration).HasMaxLength(50);
            e.Property(d => d.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
            e.HasOne(d => d.Lead).WithMany(l => l.Deals).HasForeignKey(d => d.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Fo).WithMany(u => u.Deals).HasForeignKey(d => d.FoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Approver).WithMany().HasForeignKey(d => d.ApproverId).OnDelete(DeleteBehavior.SetNull);
        });

        // Notification
        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(n => n.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(n => n.Title).HasMaxLength(200);
            e.HasOne(n => n.User).WithMany(u => u.Notifications).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        // TaskItem
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.School).HasMaxLength(200);
            e.HasOne(t => t.User).WithMany(u => u.Tasks).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Lead).WithMany().HasForeignKey(t => t.LeadId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
