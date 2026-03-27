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
    public DbSet<School> Schools => Set<School>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<GeofenceEvent> GeofenceEvents => Set<GeofenceEvent>();
    public DbSet<SchoolVisitLog> SchoolVisitLogs => Set<SchoolVisitLog>();
    public DbSet<VisitReport> VisitReports => Set<VisitReport>();
    public DbSet<VisitFieldConfig> VisitFieldConfigs => Set<VisitFieldConfig>();
    public DbSet<DemoAssignment> DemoAssignments => Set<DemoAssignment>();
    public DbSet<OnboardAssignment> OnboardAssignments => Set<OnboardAssignment>();
    public DbSet<DailyRoutePlan> DailyRoutePlans => Set<DailyRoutePlan>();
    public DbSet<AllowanceConfig> AllowanceConfigs => Set<AllowanceConfig>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<UserReassignment> UserReassignments => Set<UserReassignment>();
    public DbSet<DirectPayment> DirectPayments => Set<DirectPayment>();
    public DbSet<SchoolAssignment> SchoolAssignments => Set<SchoolAssignment>();
    public DbSet<AiReport> AiReports => Set<AiReport>();
    public DbSet<DeviceLogin> DeviceLogins => Set<DeviceLogin>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<DeviceFraudAlert> DeviceFraudAlerts => Set<DeviceFraudAlert>();
    public DbSet<SchoolSubscription> SchoolSubscriptions => Set<SchoolSubscription>();
    public DbSet<WeeklyPlan> WeeklyPlans => Set<WeeklyPlan>();

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

        // School
        modelBuilder.Entity<School>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.Address).HasMaxLength(500);
            e.Property(s => s.City).HasMaxLength(100);
            e.Property(s => s.State).HasMaxLength(100);
            e.Property(s => s.Pincode).HasMaxLength(10);
            e.Property(s => s.Board).HasMaxLength(50);
            e.Property(s => s.Type).HasMaxLength(50);
            e.Property(s => s.Latitude).HasColumnType("decimal(10,7)");
            e.Property(s => s.Longitude).HasColumnType("decimal(10,7)");
            e.Property(s => s.Phone).HasMaxLength(30);
            e.Property(s => s.Email).HasMaxLength(150);
            e.Property(s => s.Website).HasMaxLength(300);
            e.Property(s => s.PrincipalName).HasMaxLength(150);
            e.Property(s => s.PrincipalPhone).HasMaxLength(30);
            e.HasIndex(s => new { s.Name, s.City });
            e.HasIndex(s => s.City);
            e.HasIndex(s => s.IsActive);
        });

        // Contact
        modelBuilder.Entity<Contact>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(150).IsRequired();
            e.Property(c => c.Designation).HasMaxLength(100);
            e.Property(c => c.Department).HasMaxLength(100);
            e.Property(c => c.Phone).HasMaxLength(30);
            e.Property(c => c.AltPhone).HasMaxLength(30);
            e.Property(c => c.Email).HasMaxLength(150);
            e.Property(c => c.Profession).HasMaxLength(100);
            e.Property(c => c.PersonalityNotes).HasMaxLength(1000);
            e.HasIndex(c => c.SchoolId);
            e.HasIndex(c => c.Phone);
            e.HasOne(c => c.School).WithMany(s => s.Contacts).HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Cascade);
        });

        // GeofenceEvent
        modelBuilder.Entity<GeofenceEvent>(e =>
        {
            e.Property(g => g.EventType).HasConversion<string>().HasMaxLength(10);
            e.Property(g => g.Latitude).HasColumnType("decimal(10,7)");
            e.Property(g => g.Longitude).HasColumnType("decimal(10,7)");
            e.Property(g => g.DistanceFromSchoolMetres).HasColumnType("decimal(10,2)");
            e.HasIndex(g => new { g.SessionId, g.RecordedAt });
            e.HasIndex(g => new { g.UserId, g.RecordedAt });
            e.HasOne(g => g.Session).WithMany().HasForeignKey(g => g.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(g => g.User).WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(g => g.School).WithMany().HasForeignKey(g => g.SchoolId).OnDelete(DeleteBehavior.Cascade);
        });

        // SchoolVisitLog
        modelBuilder.Entity<SchoolVisitLog>(e =>
        {
            e.Property(v => v.DurationMinutes).HasColumnType("decimal(10,2)");
            e.HasIndex(v => new { v.UserId, v.VisitDate });
            e.HasIndex(v => new { v.SchoolId, v.VisitDate });
            e.HasIndex(v => v.SessionId);
            e.HasOne(v => v.Session).WithMany().HasForeignKey(v => v.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.School).WithMany(s => s.VisitLogs).HasForeignKey(v => v.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.EnterEvent).WithMany().HasForeignKey(v => v.EnterEventId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(v => v.ExitEvent).WithMany().HasForeignKey(v => v.ExitEventId).OnDelete(DeleteBehavior.SetNull);
        });

        // VisitReport
        modelBuilder.Entity<VisitReport>(e =>
        {
            e.Property(v => v.Purpose).HasConversion<string>().HasMaxLength(20);
            e.Property(v => v.NextAction).HasConversion<string>().HasMaxLength(20);
            e.Property(v => v.Outcome).HasMaxLength(20);
            e.Property(v => v.Remarks).HasMaxLength(2000);
            e.Property(v => v.NextActionNotes).HasMaxLength(500);
            e.HasIndex(v => v.UserId);
            e.HasIndex(v => v.SchoolId);
            e.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.School).WithMany().HasForeignKey(v => v.SchoolId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(v => v.SchoolVisitLog).WithMany().HasForeignKey(v => v.SchoolVisitLogId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(v => v.Activity).WithMany().HasForeignKey(v => v.ActivityId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(v => v.PersonMet).WithMany().HasForeignKey(v => v.PersonMetId).OnDelete(DeleteBehavior.SetNull);
        });

        // VisitFieldConfig
        modelBuilder.Entity<VisitFieldConfig>(e =>
        {
            e.Property(f => f.FieldName).HasMaxLength(100);
            e.Property(f => f.FieldType).HasMaxLength(20);
            e.HasOne(f => f.CreatedBy).WithMany().HasForeignKey(f => f.CreatedById).OnDelete(DeleteBehavior.Cascade);
        });

        // DemoAssignment
        modelBuilder.Entity<DemoAssignment>(e =>
        {
            e.Property(d => d.DemoMode).HasMaxLength(20);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.Outcome).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.Notes).HasMaxLength(2000);
            e.Property(d => d.Feedback).HasMaxLength(2000);
            e.Property(d => d.FeedbackSentiment).HasMaxLength(20);
            e.Property(d => d.FeedbackAudioUrl).HasMaxLength(500);
            e.Property(d => d.FeedbackVideoUrl).HasMaxLength(500);
            e.Property(d => d.ScreenRecordingUrl).HasMaxLength(500);
            e.Property(d => d.MeetingLink).HasMaxLength(500);
            e.HasIndex(d => new { d.AssignedToId, d.ScheduledDate });
            e.HasIndex(d => d.Status);
            e.HasOne(d => d.Lead).WithMany().HasForeignKey(d => d.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.RequestedBy).WithMany().HasForeignKey(d => d.RequestedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.AssignedTo).WithMany().HasForeignKey(d => d.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.ApprovedBy).WithMany().HasForeignKey(d => d.ApprovedById).OnDelete(DeleteBehavior.SetNull);
        });

        // OnboardAssignment
        modelBuilder.Entity<OnboardAssignment>(e =>
        {
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Notes).HasMaxLength(2000);
            e.HasIndex(o => o.AssignedToId);
            e.HasIndex(o => o.Status);
            e.HasOne(o => o.Lead).WithMany().HasForeignKey(o => o.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.Deal).WithMany().HasForeignKey(o => o.DealId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.School).WithMany().HasForeignKey(o => o.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.AssignedTo).WithMany().HasForeignKey(o => o.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.AssignedBy).WithMany().HasForeignKey(o => o.AssignedById).OnDelete(DeleteBehavior.Restrict);
        });

        // DailyRoutePlan
        modelBuilder.Entity<DailyRoutePlan>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.OptimizationMethod).HasMaxLength(20);
            e.Property(r => r.TotalEstimatedDistanceKm).HasColumnType("decimal(10,3)");
            e.Property(r => r.TotalActualDistanceKm).HasColumnType("decimal(10,3)");
            e.HasIndex(r => new { r.UserId, r.PlanDate });
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // AllowanceConfig
        modelBuilder.Entity<AllowanceConfig>(e =>
        {
            e.Property(a => a.Scope).HasConversion<string>().HasMaxLength(10);
            e.Property(a => a.RatePerKm).HasColumnType("decimal(6,2)");
            e.Property(a => a.MaxDailyAllowance).HasColumnType("decimal(10,2)");
            e.Property(a => a.MinDistanceForAllowance).HasColumnType("decimal(6,2)");
            e.HasIndex(a => new { a.Scope, a.ScopeId });
            e.HasOne(a => a.SetBy).WithMany().HasForeignKey(a => a.SetById).OnDelete(DeleteBehavior.Cascade);
        });

        // Payment
        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            e.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.TransactionId).HasMaxLength(100);
            e.Property(p => p.ChequeNumber).HasMaxLength(50);
            e.Property(p => p.BankName).HasMaxLength(100);
            e.Property(p => p.UpiId).HasMaxLength(100);
            e.Property(p => p.Notes).HasMaxLength(500);
            e.HasIndex(p => p.DealId);
            e.HasIndex(p => p.Status);
            e.HasOne(p => p.Deal).WithMany().HasForeignKey(p => p.DealId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.School).WithMany().HasForeignKey(p => p.SchoolId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.CollectedBy).WithMany().HasForeignKey(p => p.CollectedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.VerifiedBy).WithMany().HasForeignKey(p => p.VerifiedById).OnDelete(DeleteBehavior.SetNull);
        });

        // CalendarEvent
        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.Property(c => c.EventType).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Title).HasMaxLength(200);
            e.Property(c => c.Description).HasMaxLength(1000);
            e.HasIndex(c => new { c.UserId, c.StartTime });
            e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Lead).WithMany().HasForeignKey(c => c.LeadId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.DemoAssignment).WithMany().HasForeignKey(c => c.DemoAssignmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.OnboardAssignment).WithMany().HasForeignKey(c => c.OnboardAssignmentId).OnDelete(DeleteBehavior.SetNull);
        });

        // UserReassignment
        modelBuilder.Entity<UserReassignment>(e =>
        {
            e.Property(r => r.EntityType).HasMaxLength(50);
            e.Property(r => r.Notes).HasMaxLength(500);
            e.HasOne(r => r.OldUser).WithMany().HasForeignKey(r => r.OldUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.NewUser).WithMany().HasForeignKey(r => r.NewUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ReassignedBy).WithMany().HasForeignKey(r => r.ReassignedById).OnDelete(DeleteBehavior.Restrict);
        });

        // DirectPayment
        modelBuilder.Entity<DirectPayment>(e =>
        {
            e.Property(d => d.Amount).HasColumnType("decimal(18,2)");
            e.Property(d => d.Method).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.Purpose).HasMaxLength(100);
            e.Property(d => d.TransactionId).HasMaxLength(200);
            e.Property(d => d.UpiId).HasMaxLength(100);
            e.Property(d => d.BankName).HasMaxLength(100);
            e.Property(d => d.Notes).HasMaxLength(500);
            e.HasOne(d => d.Recipient).WithMany().HasForeignKey(d => d.RecipientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.PaidBy).WithMany().HasForeignKey(d => d.PaidById).OnDelete(DeleteBehavior.Restrict);
        });

        // AiReport
        modelBuilder.Entity<AiReport>(e =>
        {
            e.Property(r => r.ReportType).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.OverallRating).HasMaxLength(20);
            e.Property(r => r.ErrorMessage).HasMaxLength(2000);
            e.Property(r => r.InputDataJson).HasColumnType("text");
            e.Property(r => r.OutputJson).HasColumnType("text");
            e.HasIndex(r => new { r.UserId, r.ReportType, r.ReportDate });
            e.HasIndex(r => r.ReportDate);
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // SchoolAssignment
        modelBuilder.Entity<SchoolAssignment>(e =>
        {
            e.Property(a => a.TimeSpentMinutes).HasColumnType("decimal(10,2)");
            e.Property(a => a.Notes).HasMaxLength(500);
            e.HasIndex(a => new { a.UserId, a.AssignmentDate });
            e.HasIndex(a => new { a.SchoolId, a.AssignmentDate });
            e.HasOne(a => a.School).WithMany().HasForeignKey(a => a.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.AssignedBy).WithMany().HasForeignKey(a => a.AssignedById).OnDelete(DeleteBehavior.Restrict);
        });

        // DeviceLogin
        modelBuilder.Entity<DeviceLogin>(e =>
        {
            e.Property(d => d.DeviceFingerprint).HasMaxLength(128);
            e.Property(d => d.DeviceUniqueId).HasMaxLength(256);
            e.Property(d => d.DeviceBrand).HasMaxLength(100);
            e.Property(d => d.DeviceModel).HasMaxLength(100);
            e.Property(d => d.DeviceOs).HasMaxLength(100);
            e.Property(d => d.AppVersion).HasMaxLength(50);
            e.Property(d => d.SimCarrier).HasMaxLength(100);
            e.Property(d => d.IpAddress).HasMaxLength(50);
            e.Property(d => d.UserAgent).HasMaxLength(500);
            e.HasIndex(d => new { d.UserId, d.LoginAt });
            e.HasIndex(d => new { d.DeviceFingerprint, d.LoginAt });
            e.HasIndex(d => d.IpAddress);
            e.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // UserDevice
        modelBuilder.Entity<UserDevice>(e =>
        {
            e.Property(d => d.DeviceFingerprint).HasMaxLength(128);
            e.Property(d => d.DeviceUniqueId).HasMaxLength(256);
            e.Property(d => d.DeviceBrand).HasMaxLength(100);
            e.Property(d => d.DeviceModel).HasMaxLength(100);
            e.Property(d => d.DeviceOs).HasMaxLength(100);
            e.Property(d => d.TrustLevel).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(d => new { d.UserId, d.DeviceFingerprint }).IsUnique();
            e.HasIndex(d => d.DeviceFingerprint);
            e.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // DeviceFraudAlert
        modelBuilder.Entity<DeviceFraudAlert>(e =>
        {
            e.Property(a => a.FraudType).HasConversion<string>().HasMaxLength(50);
            e.Property(a => a.Severity).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.DeviceFingerprint).HasMaxLength(128);
            e.Property(a => a.Title).HasMaxLength(500);
            e.Property(a => a.Description).HasColumnType("text");
            e.Property(a => a.EvidenceJson).HasColumnType("text");
            e.Property(a => a.ReviewNotes).HasMaxLength(1000);
            e.HasIndex(a => new { a.UserId, a.DetectedAt });
            e.HasIndex(a => a.Status);
            e.HasIndex(a => new { a.FraudType, a.DetectedAt });
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.OtherUser).WithMany().HasForeignKey(a => a.OtherUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.ReviewedBy).WithMany().HasForeignKey(a => a.ReviewedById).OnDelete(DeleteBehavior.SetNull);
        });

        // SchoolSubscription
        modelBuilder.Entity<SchoolSubscription>(e =>
        {
            e.Property(s => s.PlanType).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.CredentialStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.SchoolLoginEmail).HasMaxLength(200);
            e.Property(s => s.SchoolLoginPassword).HasMaxLength(200);
            e.Property(s => s.Modules).HasColumnType("text");
            e.Property(s => s.Amount).HasColumnType("decimal(18,2)");
            e.Property(s => s.Notes).HasMaxLength(1000);
            e.HasIndex(s => new { s.DealId }).IsUnique();
            e.HasIndex(s => s.SchoolId);
            e.HasIndex(s => s.Status);
            e.HasOne(s => s.Deal).WithMany().HasForeignKey(s => s.DealId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.School).WithMany().HasForeignKey(s => s.SchoolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.CredentialProvisionedBy).WithMany().HasForeignKey(s => s.CredentialProvisionedById).OnDelete(DeleteBehavior.SetNull);
        });

        // WeeklyPlan
        modelBuilder.Entity<WeeklyPlan>(e =>
        {
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(w => w.PlanData).HasColumnType("text");
            e.Property(w => w.ManagerEdits).HasColumnType("text");
            e.Property(w => w.ReviewNotes).HasMaxLength(1000);
            e.HasIndex(w => new { w.UserId, w.WeekStartDate }).IsUnique();
            e.HasIndex(w => w.Status);
            e.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.ReviewedBy).WithMany().HasForeignKey(w => w.ReviewedById).OnDelete(DeleteBehavior.SetNull);
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
