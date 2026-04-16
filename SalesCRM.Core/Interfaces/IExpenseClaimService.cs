using SalesCRM.Core.DTOs.Expense;

namespace SalesCRM.Core.Interfaces;

public interface IExpenseClaimService
{
    Task<ExpenseClaimDto> CreateClaimAsync(CreateExpenseClaimRequest request, string? billUrl, int userId);
    Task<List<ExpenseClaimDto>> GetMyClaimsAsync(int userId, string? status, string? category, DateTime? from, DateTime? to);
    Task<List<ExpenseClaimDto>> GetTeamClaimsAsync(int managerId, string role, string? status, string? category, DateTime? from, DateTime? to);
    Task<ExpenseClaimDto?> ApproveClaimAsync(int id, int approverId);
    Task<ExpenseClaimDto?> RejectClaimAsync(int id, RejectExpenseClaimRequest request, int approverId);
    Task<int> BulkApproveClaimsAsync(List<int> ids, int approverId);
}
