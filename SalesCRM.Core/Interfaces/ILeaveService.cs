using SalesCRM.Core.DTOs.Leave;

namespace SalesCRM.Core.Interfaces;

public interface ILeaveService
{
    Task<LeaveRequestDto> ApplyLeaveAsync(ApplyLeaveRequest request, int userId);
    Task<List<LeaveRequestDto>> GetMyLeavesAsync(int userId, string? status, string? category, DateTime? from, DateTime? to);
    Task<List<LeaveRequestDto>> GetTeamLeavesAsync(int managerId, string role, string? status, string? category, DateTime? from, DateTime? to, int? filterUserId = null);
    Task<LeaveRequestDto?> ApproveLeaveAsync(int leaveId, int approverId);
    Task<LeaveRequestDto?> RejectLeaveAsync(int leaveId, RejectLeaveRequest request, int approverId);
    Task<LeaveRequestDto?> CancelLeaveAsync(int leaveId, int userId);
}
