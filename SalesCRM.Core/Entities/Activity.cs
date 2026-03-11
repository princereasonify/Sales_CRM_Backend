using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class Activity : BaseEntity
{
    public ActivityType Type { get; set; }
    public DateTime Date { get; set; }
    public ActivityOutcome Outcome { get; set; }
    public string? Notes { get; set; }
    public bool GpsVerified { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Visit tracker fields
    public DateTime? TimeIn { get; set; }
    public DateTime? TimeOut { get; set; }
    public string? PersonMet { get; set; }
    public string? PersonDesignation { get; set; }
    public string? PersonPhone { get; set; }
    public string? InterestLevel { get; set; } // High, Medium, Low
    public string? NextAction { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public string? PhotoUrl { get; set; }

    // Demo fields
    public string? DemoMode { get; set; } // Online, Offline
    public string? ConductedBy { get; set; }
    public int? Attendees { get; set; }
    public string? Feedback { get; set; }

    public int FoId { get; set; }
    public User Fo { get; set; } = null!;

    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;
}
