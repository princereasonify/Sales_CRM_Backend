namespace SalesCRM.Core.DTOs;

public class ActivityDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool GpsVerified { get; set; }
    public int FoId { get; set; }
    public string FoName { get; set; } = string.Empty;
    public int LeadId { get; set; }
    public string School { get; set; } = string.Empty;
}

public class CreateActivityRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool GpsVerified { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int LeadId { get; set; }
}
