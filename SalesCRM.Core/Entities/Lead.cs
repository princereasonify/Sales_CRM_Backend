using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class Lead : BaseEntity
{
    public string School { get; set; } = string.Empty;
    public string Board { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Students { get; set; }
    public string Type { get; set; } = string.Empty;  // Private, Government, Franchise, Trust
    public LeadStage Stage { get; set; } = LeadStage.NewLead;
    public int Score { get; set; }
    public decimal Value { get; set; }
    public DateTime? CloseDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? LossReason { get; set; }

    // Contact (Decision Maker)
    public string ContactName { get; set; } = string.Empty;
    public string ContactDesignation { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    // Foreign Keys
    public int FoId { get; set; }
    public User Fo { get; set; } = null!;

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
