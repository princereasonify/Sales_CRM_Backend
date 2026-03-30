namespace SalesCRM.Core.Options;

public class AiReportOptions
{
    public const string SectionName = "AiReports";

    public string FoDailyReportTimeIst { get; set; } = "20:00";
    public string ManagementReportDayOfWeek { get; set; } = "Saturday";
    public string ManagementReportTimeIst { get; set; } = "18:00";
}
