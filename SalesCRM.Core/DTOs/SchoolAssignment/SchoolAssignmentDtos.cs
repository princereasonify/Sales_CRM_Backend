namespace SalesCRM.Core.DTOs.SchoolAssignment;

public class SchoolAssignmentDto
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string? SchoolAddress { get; set; }
    public string? SchoolCity { get; set; }
    public decimal SchoolLatitude { get; set; }
    public decimal SchoolLongitude { get; set; }
    public int GeofenceRadiusMetres { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int AssignedById { get; set; }
    public string AssignedByName { get; set; } = string.Empty;
    public string AssignmentDate { get; set; } = string.Empty;
    public int VisitOrder { get; set; }
    public bool IsVisited { get; set; }
    public DateTime? VisitedAt { get; set; }
    public decimal? TimeSpentMinutes { get; set; }
    public string? Notes { get; set; }
}

public class CreateSchoolAssignmentRequest
{
    public int SchoolId { get; set; }
    public int UserId { get; set; }
    public string AssignmentDate { get; set; } = string.Empty; // yyyy-MM-dd
    public int VisitOrder { get; set; }
    public string? Notes { get; set; }
}

public class BulkAssignRequest
{
    public int UserId { get; set; }
    public string AssignmentDate { get; set; } = string.Empty;
    public List<int> SchoolIds { get; set; } = new();
    public string? Notes { get; set; }
}

public class AssignmentListResponse
{
    public bool Success { get; set; } = true;
    public List<SchoolAssignmentDto> Assignments { get; set; } = new();
}
