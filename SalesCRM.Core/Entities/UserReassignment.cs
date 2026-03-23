namespace SalesCRM.Core.Entities;

public class UserReassignment : BaseEntity
{
    public int OldUserId { get; set; }
    public int NewUserId { get; set; }
    public string EntityType { get; set; } = string.Empty; // Lead, Contact, School
    public int EntityId { get; set; }
    public int ReassignedById { get; set; }
    public string? Notes { get; set; }

    public User OldUser { get; set; } = null!;
    public User NewUser { get; set; } = null!;
    public User ReassignedBy { get; set; } = null!;
}
