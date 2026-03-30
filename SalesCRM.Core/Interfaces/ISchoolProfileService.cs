using SalesCRM.Core.DTOs.SchoolProfile;

namespace SalesCRM.Core.Interfaces;

public interface ISchoolProfileService
{
    Task<List<SchoolProfileDto>> GetAllAsync();
    Task<SchoolProfileDto?> GetByIdAsync(int id);
    Task<SchoolProfileDto> CreateAsync(CreateSchoolProfileRequest request, int createdById, string exportFormat);
    Task<SchoolProfileDto?> UpdateAsync(int id, UpdateSchoolProfileRequest request, string exportFormat);
    Task<SchoolProfilePrefillDto> GetPrefillAsync(int schoolId);
    Task<List<OnboardedSchoolDto>> GetOnboardedSchoolsAsync();
    Task<string> ExportCsvAsync();
}
