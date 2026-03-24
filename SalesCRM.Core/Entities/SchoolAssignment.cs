namespace SalesCRM.Core.Entities;

public class SchoolAssignment : BaseEntity
{
    public int SchoolId { get; set; }
    public int UserId { get; set; }          // FO assigned to visit
    public int AssignedById { get; set; }     // Manager who assigned
    public DateTime AssignmentDate { get; set; } // Date the FO should visit
    public int VisitOrder { get; set; }       // Suggested visit sequence (1, 2, 3...)
    public bool IsVisited { get; set; }       // Marked true when geofence enter detected
    public DateTime? VisitedAt { get; set; }  // When geofence enter was first detected
    public decimal? TimeSpentMinutes { get; set; } // Calculated from geofence enter/exit
    public string? Notes { get; set; }

    // Navigation
    public School School { get; set; } = null!;
    public User User { get; set; } = null!;
    public User AssignedBy { get; set; } = null!;
}
