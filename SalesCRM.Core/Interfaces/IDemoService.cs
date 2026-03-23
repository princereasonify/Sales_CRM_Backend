using SalesCRM.Core.DTOs.Demos;

namespace SalesCRM.Core.Interfaces;

public interface IDemoService
{
    Task<(List<DemoAssignmentDto> Demos, int Total)> GetDemosAsync(string? status, int? assignedToId, string? from, string? to, int page, int limit);
    Task<DemoAssignmentDto?> GetDemoByIdAsync(int id);
    Task<DemoAssignmentDto> CreateDemoAsync(CreateDemoRequest request, int requestedById);
    Task<DemoAssignmentDto?> UpdateDemoAsync(int id, UpdateDemoRequest request);
    Task<List<DemoAssignmentDto>> GetDemoCalendarAsync(string from, string to, int userId);
}
