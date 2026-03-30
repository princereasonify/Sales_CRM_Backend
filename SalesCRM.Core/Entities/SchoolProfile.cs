namespace SalesCRM.Core.Entities;

public class SchoolProfile : BaseEntity
{
    public int SchoolId { get; set; }
    public School School { get; set; } = null!;

    // User Information (school portal admin)
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserPhone { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;

    // School Information
    public string SchoolName { get; set; } = string.Empty;
    public string SchoolAddress { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string SchoolPhone { get; set; } = string.Empty;
    public string SchoolEmail { get; set; } = string.Empty;
    public string Zipcode { get; set; } = string.Empty;
    public string? SchoolLogo { get; set; } // Base64 encoded image

    // FO who handled the deal
    public string FoName { get; set; } = string.Empty;
    public string FoEmail { get; set; } = string.Empty;

    // Who created this profile
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
}
