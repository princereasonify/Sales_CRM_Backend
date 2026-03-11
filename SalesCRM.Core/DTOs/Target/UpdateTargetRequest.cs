namespace SalesCRM.Core.DTOs.Target;

public class UpdateTargetRequest
{
    public decimal AchievedAmount { get; set; }
    public int AchievedSchools { get; set; }
    public int? AchievedLogins { get; set; }
    public int? AchievedStudents { get; set; }
    public string Status { get; set; } = string.Empty;
}
