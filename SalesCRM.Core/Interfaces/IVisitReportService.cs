using SalesCRM.Core.DTOs.VisitReports;

namespace SalesCRM.Core.Interfaces;

public interface IVisitReportService
{
    Task<VisitReportDto> CreateVisitReportAsync(CreateVisitReportRequest request, int userId);
    Task<List<VisitReportDto>> GetVisitReportsByUserAsync(int userId, string? date);
    Task<List<VisitFieldConfigDto>> GetVisitFieldConfigsAsync();
    Task<VisitFieldConfigDto> CreateVisitFieldConfigAsync(CreateVisitFieldConfigRequest request, int createdById);
    Task<VisitFieldConfigDto?> UpdateVisitFieldConfigAsync(int id, CreateVisitFieldConfigRequest request);
    Task<bool> DeleteVisitFieldConfigAsync(int id);
}
