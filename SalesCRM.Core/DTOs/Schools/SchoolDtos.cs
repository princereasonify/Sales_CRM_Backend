namespace SalesCRM.Core.DTOs.Schools;

public class SchoolDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Board { get; set; }
    public string? Type { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int GeofenceRadiusMetres { get; set; }
    public int? StudentCount { get; set; }
    public int? StaffCount { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PrincipalName { get; set; }
    public string? PrincipalPhone { get; set; }
    public bool IsActive { get; set; }
    public int ContactCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SchoolListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Board { get; set; }
    public string? Type { get; set; }
    public int? StudentCount { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int GeofenceRadiusMetres { get; set; }
    public bool IsActive { get; set; }
    public int ContactCount { get; set; }
}

public class SchoolGeofenceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int GeofenceRadiusMetres { get; set; }
}

public class CreateSchoolRequest
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
}

public class UpdateSchoolRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Board { get; set; }
    public string? Type { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? GeofenceRadiusMetres { get; set; }
    public int? StudentCount { get; set; }
    public int? StaffCount { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PrincipalName { get; set; }
    public string? PrincipalPhone { get; set; }
    public bool? IsActive { get; set; }
}
