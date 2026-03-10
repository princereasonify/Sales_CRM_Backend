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

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
