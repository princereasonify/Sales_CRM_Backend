namespace SalesCRM.Core.Entities;

public class Contact : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? AltPhone { get; set; }
    public string? Email { get; set; }
    public string? Profession { get; set; }
    public string? PersonalityNotes { get; set; }
    public bool IsDecisionMaker { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public School School { get; set; } = null!;
}
