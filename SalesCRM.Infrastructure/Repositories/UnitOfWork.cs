using SalesCRM.Core.Entities;
using SalesCRM.Core.Interfaces;
using SalesCRM.Infrastructure.Data;

namespace SalesCRM.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Users = new Repository<User>(context);
        Leads = new Repository<Lead>(context);
        Activities = new Repository<Activity>(context);
        Deals = new Repository<Deal>(context);
        Notifications = new Repository<Notification>(context);
        Tasks = new Repository<TaskItem>(context);
        Regions = new Repository<Region>(context);
        Zones = new Repository<Zone>(context);
        TargetAssignments = new Repository<TargetAssignment>(context);
        TrackingSessions = new Repository<TrackingSession>(context);
        LocationPings = new Repository<LocationPing>(context);
        DailyAllowances = new Repository<DailyAllowance>(context);
        Schools = new Repository<School>(context);
        Contacts = new Repository<Contact>(context);
        GeofenceEvents = new Repository<GeofenceEvent>(context);
        SchoolVisitLogs = new Repository<SchoolVisitLog>(context);
        VisitReports = new Repository<VisitReport>(context);
        VisitFieldConfigs = new Repository<VisitFieldConfig>(context);
        DemoAssignments = new Repository<DemoAssignment>(context);
        OnboardAssignments = new Repository<OnboardAssignment>(context);
        DailyRoutePlans = new Repository<DailyRoutePlan>(context);
        AllowanceConfigs = new Repository<AllowanceConfig>(context);
        Payments = new Repository<Payment>(context);
        CalendarEvents = new Repository<CalendarEvent>(context);
        UserReassignments = new Repository<UserReassignment>(context);
        DirectPayments = new Repository<DirectPayment>(context);
        SchoolAssignments = new Repository<SchoolAssignment>(context);
        AiReports = new Repository<AiReport>(context);
    }

    public IRepository<User> Users { get; }
    public IRepository<Lead> Leads { get; }
    public IRepository<Activity> Activities { get; }
    public IRepository<Deal> Deals { get; }
    public IRepository<Notification> Notifications { get; }
    public IRepository<TaskItem> Tasks { get; }
    public IRepository<Region> Regions { get; }
    public IRepository<Zone> Zones { get; }
    public IRepository<TargetAssignment> TargetAssignments { get; }
    public IRepository<TrackingSession> TrackingSessions { get; }
    public IRepository<LocationPing> LocationPings { get; }
    public IRepository<DailyAllowance> DailyAllowances { get; }
    public IRepository<School> Schools { get; }
    public IRepository<Contact> Contacts { get; }
    public IRepository<GeofenceEvent> GeofenceEvents { get; }
    public IRepository<SchoolVisitLog> SchoolVisitLogs { get; }
    public IRepository<VisitReport> VisitReports { get; }
    public IRepository<VisitFieldConfig> VisitFieldConfigs { get; }
    public IRepository<DemoAssignment> DemoAssignments { get; }
    public IRepository<OnboardAssignment> OnboardAssignments { get; }
    public IRepository<DailyRoutePlan> DailyRoutePlans { get; }
    public IRepository<AllowanceConfig> AllowanceConfigs { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<CalendarEvent> CalendarEvents { get; }
    public IRepository<UserReassignment> UserReassignments { get; }
    public IRepository<DirectPayment> DirectPayments { get; }
    public IRepository<SchoolAssignment> SchoolAssignments { get; }
    public IRepository<AiReport> AiReports { get; }

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
