namespace SalesCRM.Core.DTOs.Auth;

public class CreateZoneRequest
{
    public string Name { get; set; } = string.Empty;
    public int RegionId { get; set; }
}
