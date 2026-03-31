namespace SalesCRM.Core.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public int? ZoneId { get; set; }
    public string? Zone { get; set; }
    public int? RegionId { get; set; }
    public string? Region { get; set; }
    public string? ZonalHead { get; set; }
    public string? RegionalHead { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
}
