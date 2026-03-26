using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class SchoolSubscription : BaseEntity
{
    public int DealId { get; set; }
    public Deal Deal { get; set; } = null!;

    public int? SchoolId { get; set; }
    public School? School { get; set; }

    public PlanType PlanType { get; set; } = PlanType.Annually;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;

    public string? SchoolLoginEmail { get; set; }
    public string? SchoolLoginPassword { get; set; }
    public CredentialStatus CredentialStatus { get; set; } = CredentialStatus.NotProvisioned;
    public DateTime? CredentialProvisionedAt { get; set; }
    public int? CredentialProvisionedById { get; set; }
    public User? CredentialProvisionedBy { get; set; }

    public int NumberOfLicenses { get; set; }
    public string? Modules { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
