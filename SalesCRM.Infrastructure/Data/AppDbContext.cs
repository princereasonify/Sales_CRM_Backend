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
    public DbSet<TargetAssignment> TargetAssignments => Set<TargetAssignment>();
    public DbSet<TrackingSession> TrackingSessions => Set<TrackingSession>();
    public DbSet<LocationPing> LocationPings => Set<LocationPing>();
    public DbSet<DailyAllowance> DailyAllowances => Set<DailyAllowance>();

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
            e.Property(u => u.TravelAllowanceRate).HasColumnType("decimal(6,2)").HasDefaultValue(10.00m);
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
            e.HasOne(l => l.AssignedBy).WithMany().HasForeignKey(l => l.AssignedById).OnDelete(DeleteBehavior.SetNull);
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

        // TargetAssignment
        modelBuilder.Entity<TargetAssignment>(e =>
        {
            e.Property(t => t.Title).HasMaxLength(200);
            e.Property(t => t.Description).HasMaxLength(1000);
            e.Property(t => t.TargetAmount).HasColumnType("decimal(18,2)");
            e.Property(t => t.AchievedAmount).HasColumnType("decimal(18,2)");
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.PeriodType).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.ReviewNote).HasMaxLength(500);
            e.HasOne(t => t.AssignedTo).WithMany().HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.AssignedBy).WithMany().HasForeignKey(t => t.AssignedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ParentTarget).WithMany(t => t.SubTargets).HasForeignKey(t => t.ParentTargetId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(t => t.AssignedToId);
            e.HasIndex(t => t.AssignedById);
        });

        // TrackingSession
        modelBuilder.Entity<TrackingSession>(e =>
        {
            e.Property(t => t.Role).HasConversion<string>().HasMaxLength(10);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.TotalDistanceKm).HasColumnType("decimal(10,3)");
            e.Property(t => t.AllowanceAmount).HasColumnType("decimal(10,2)");
            e.Property(t => t.AllowanceRatePerKm).HasColumnType("decimal(6,2)");
            e.Property(t => t.RawDistanceKm).HasColumnType("decimal(10,3)");
            e.Property(t => t.FilteredDistanceKm).HasColumnType("decimal(10,3)");
            e.Property(t => t.ReconstructedDistanceKm).HasColumnType("decimal(10,3)");
            e.Property(t => t.FraudFlags).HasMaxLength(1000);
            e.HasIndex(t => new { t.UserId, t.SessionDate });
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.SessionDate);
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // LocationPing
        modelBuilder.Entity<LocationPing>(e =>
        {
            e.Property(p => p.Latitude).HasColumnType("decimal(10,7)");
            e.Property(p => p.Longitude).HasColumnType("decimal(10,7)");
            e.Property(p => p.AccuracyMetres).HasColumnType("decimal(8,2)");
            e.Property(p => p.SpeedKmh).HasColumnType("decimal(8,2)");
            e.Property(p => p.AltitudeMetres).HasColumnType("decimal(8,2)");
            e.Property(p => p.DistanceFromPrevKm).HasColumnType("decimal(10,5)");
            e.Property(p => p.CumulativeDistanceKm).HasColumnType("decimal(10,3)");
            e.Property(p => p.InvalidReason).HasMaxLength(100);
            e.Property(p => p.Provider).HasMaxLength(20);
            e.Property(p => p.BatteryLevel).HasColumnType("decimal(5,2)");
            e.Property(p => p.FilterReason).HasMaxLength(100);
            e.HasIndex(p => p.SessionId);
            e.HasIndex(p => p.UserId);
            e.HasIndex(p => p.RecordedAt);
            e.HasOne(p => p.Session).WithMany(s => s.LocationPings).HasForeignKey(p => p.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // DailyAllowance
        modelBuilder.Entity<DailyAllowance>(e =>
        {
            e.Property(a => a.TotalDistanceKm).HasColumnType("decimal(10,3)");
            e.Property(a => a.RatePerKm).HasColumnType("decimal(6,2)");
            e.Property(a => a.GrossAllowance).HasColumnType("decimal(10,2)");
            e.Property(a => a.Remarks).HasMaxLength(500);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.AllowanceDate);
            e.HasIndex(a => a.SessionId).IsUnique();
            e.HasOne(a => a.Session).WithOne(s => s.DailyAllowance).HasForeignKey<DailyAllowance>(a => a.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.ApprovedBy).WithMany().HasForeignKey(a => a.ApprovedById).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private void NormalizeDateTimesToUtc()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        // Normalize all DateTime properties to UTC for PostgreSQL
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.CurrentValue is DateTime dt && dt.Kind != DateTimeKind.Utc)
                        prop.CurrentValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDateTimesToUtc();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        NormalizeDateTimesToUtc();
        return base.SaveChanges();
    }
}
