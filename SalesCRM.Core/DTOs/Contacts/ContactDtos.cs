namespace SalesCRM.Core.DTOs.Contacts;

public class ContactDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? AltPhone { get; set; }
    public string? Email { get; set; }
    public string? Profession { get; set; }
    public string? PersonalityNotes { get; set; }
    public bool IsDecisionMaker { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContactListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public bool IsDecisionMaker { get; set; }
}

public class CreateContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? AltPhone { get; set; }
    public string? Email { get; set; }
    public string? Profession { get; set; }
    public string? PersonalityNotes { get; set; }
    public bool IsDecisionMaker { get; set; }
}

public class UpdateContactRequest
{
    public string? Name { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? AltPhone { get; set; }
    public string? Email { get; set; }
    public string? Profession { get; set; }
    public string? PersonalityNotes { get; set; }
    public bool? IsDecisionMaker { get; set; }
    public bool? IsActive { get; set; }
}
