namespace SalesCRM.Core.Enums;

public enum PlanType
{
    Monthly,
    Quarterly,
    HalfYearly,
    Annually
}

public enum SubscriptionStatus
{
    Pending,
    Active,
    Expiring,
    Expired,
    Suspended
}

public enum CredentialStatus
{
    NotProvisioned,
    Provisioned,
    Revoked
}
