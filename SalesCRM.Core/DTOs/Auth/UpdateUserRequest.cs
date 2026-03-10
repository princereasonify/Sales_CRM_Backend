namespace SalesCRM.Core.DTOs.Auth;

public class UpdateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? ZoneId { get; set; }
    public int? RegionId { get; set; }
}
