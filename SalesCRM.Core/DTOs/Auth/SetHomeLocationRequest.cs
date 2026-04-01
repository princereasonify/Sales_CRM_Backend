namespace SalesCRM.Core.DTOs.Auth;

public class SetHomeLocationRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? Address { get; set; }
}
