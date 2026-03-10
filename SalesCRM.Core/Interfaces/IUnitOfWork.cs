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
    Task<int> SaveChangesAsync();
}
