namespace SalesCRM.Core.Entities;

public class School : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Board { get; set; }
    public string? Type { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int GeofenceRadiusMetres { get; set; } = 100;
    public int? StudentCount { get; set; }
    public int? StaffCount { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PrincipalName { get; set; }
    public string? PrincipalPhone { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<SchoolVisitLog> VisitLogs { get; set; } = new List<SchoolVisitLog>();
}
