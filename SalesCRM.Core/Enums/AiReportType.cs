namespace SalesCRM.Core.Enums;

public enum AiReportType
{
    FoDaily,
    FoWeekly,
    FoMonthly,
    ZhWeekly,
    RhWeekly,
    ShWeekly,
    ScaWeekly
}

public enum AiReportStatus
{
    Pending,
    Generating,
    Completed,
    Failed
}
