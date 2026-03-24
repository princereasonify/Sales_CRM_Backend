using SalesCRM.Core.DTOs.SchoolAssignment;

namespace SalesCRM.Core.Interfaces;

public interface ISchoolAssignmentService
{
    /// <summary>Bulk assign schools to an FO for a specific date</summary>
    Task<List<SchoolAssignmentDto>> BulkAssignAsync(int assignedById, BulkAssignRequest request);

    /// <summary>Get assignments for a specific FO on a date (used by FO to see their day)</summary>
    Task<List<SchoolAssignmentDto>> GetAssignmentsAsync(int userId, string date);

    /// <summary>Get all assignments created by a manager for a date (used by managers)</summary>
    Task<List<SchoolAssignmentDto>> GetAssignmentsByManagerAsync(int managerId, string managerRole, string date);

    /// <summary>Delete an assignment</summary>
    Task<bool> DeleteAssignmentAsync(int assignmentId, int requesterId);

    /// <summary>Mark assignment visited (called internally by geofence service)</summary>
    Task MarkVisitedAsync(int userId, int schoolId, DateTime visitedAt);

    /// <summary>Update time spent (called internally when geofence exit detected)</summary>
    Task UpdateTimeSpentAsync(int userId, int schoolId, decimal durationMinutes);
}
