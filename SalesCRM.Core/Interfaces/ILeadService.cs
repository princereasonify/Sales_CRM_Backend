using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;

namespace SalesCRM.Core.Interfaces;

public interface ILeadService
{
    Task<PaginatedResult<LeadListDto>> GetLeadsAsync(int userId, PaginationParams pagination, string? search, string? stage, string? source);
    Task<LeadDto?> GetLeadByIdAsync(int id, int userId);
    Task<LeadDto> CreateLeadAsync(CreateLeadRequest request, int creatorId, string creatorRole);
    Task<LeadDto?> UpdateLeadAsync(int id, UpdateLeadRequest request, int userId);
    Task<bool> DeleteLeadAsync(int id, int userId);
    Task<bool> CheckDuplicateAsync(string school, string city);
    Task<List<LeadListDto>> GetLeadsByStageAsync(int userId);
    Task<LeadDto?> AssignLeadAsync(int leadId, AssignLeadRequest request, int assignerId, string assignerRole);
    Task<List<UserDto>> GetAssignableFosAsync(int userId, string userRole);
    Task<LeadDto?> MarkLeadLostAsync(int leadId, MarkLeadLostRequest request, int userId, string userRole);
}
