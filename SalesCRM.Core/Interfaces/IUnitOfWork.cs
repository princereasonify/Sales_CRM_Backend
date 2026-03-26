using SalesCRM.Core.Entities;

namespace SalesCRM.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Lead> Leads { get; }
    IRepository<Activity> Activities { get; }
    IRepository<Deal> Deals { get; }
    IRepository<Notification> Notifications { get; }
    IRepository<TaskItem> Tasks { get; }
    IRepository<Region> Regions { get; }
    IRepository<Zone> Zones { get; }
    IRepository<TargetAssignment> TargetAssignments { get; }
    IRepository<TrackingSession> TrackingSessions { get; }
    IRepository<LocationPing> LocationPings { get; }
    IRepository<DailyAllowance> DailyAllowances { get; }
    IRepository<School> Schools { get; }
    IRepository<Contact> Contacts { get; }
    IRepository<GeofenceEvent> GeofenceEvents { get; }
    IRepository<SchoolVisitLog> SchoolVisitLogs { get; }
    IRepository<VisitReport> VisitReports { get; }
    IRepository<VisitFieldConfig> VisitFieldConfigs { get; }
    IRepository<DemoAssignment> DemoAssignments { get; }
    IRepository<OnboardAssignment> OnboardAssignments { get; }
    IRepository<DailyRoutePlan> DailyRoutePlans { get; }
    IRepository<AllowanceConfig> AllowanceConfigs { get; }
    IRepository<Payment> Payments { get; }
    IRepository<CalendarEvent> CalendarEvents { get; }
    IRepository<UserReassignment> UserReassignments { get; }
    IRepository<DirectPayment> DirectPayments { get; }
    IRepository<SchoolAssignment> SchoolAssignments { get; }
    IRepository<AiReport> AiReports { get; }
    IRepository<DeviceLogin> DeviceLogins { get; }
    IRepository<UserDevice> UserDevices { get; }
    IRepository<DeviceFraudAlert> DeviceFraudAlerts { get; }
    IRepository<SchoolSubscription> SchoolSubscriptions { get; }
    Task<int> SaveChangesAsync();
}
