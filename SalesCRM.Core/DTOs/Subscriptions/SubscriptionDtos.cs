namespace SalesCRM.Core.DTOs.Subscriptions;

public class SchoolSubscriptionDto
{
    public int Id { get; set; }
    public int DealId { get; set; }
    public int? SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SchoolLoginEmail { get; set; }
    public string CredentialStatus { get; set; } = string.Empty;
    public DateTime? CredentialProvisionedAt { get; set; }
    public string? CredentialProvisionedByName { get; set; }
    public int NumberOfLicenses { get; set; }
    public string? Modules { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public string? DealPaymentStatus { get; set; }
    public string? FoName { get; set; }
    public int DaysRemaining { get; set; }
}

public class ProvisionCredentialsRequest
{
    public string SchoolLoginEmail { get; set; } = string.Empty;
    public string SchoolLoginPassword { get; set; } = string.Empty;
}

public class UpdateSubscriptionStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
