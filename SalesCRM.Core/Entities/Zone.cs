namespace SalesCRM.Core.Entities;

public class Zone : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public ICollection<User> Users { get; set; } = new List<User>();
}
