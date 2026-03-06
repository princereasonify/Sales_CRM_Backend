namespace SalesCRM.Core.Entities;

public class Region : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
