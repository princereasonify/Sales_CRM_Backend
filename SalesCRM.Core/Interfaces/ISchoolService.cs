using SalesCRM.Core.DTOs.Contacts;
using SalesCRM.Core.DTOs.Schools;

namespace SalesCRM.Core.Interfaces;

public interface ISchoolService
{
    Task<(List<SchoolListDto> Schools, int Total)> GetSchoolsAsync(int page, int limit, string? search, string? city, string? state, string? board);
    Task<SchoolDto?> GetSchoolByIdAsync(int id);
    Task<SchoolDto> CreateSchoolAsync(CreateSchoolRequest request);
    Task<SchoolDto?> UpdateSchoolAsync(int id, UpdateSchoolRequest request);
    Task<bool> DeleteSchoolAsync(int id);
    Task<List<SchoolGeofenceDto>> GetSchoolsForMapAsync();
    Task<List<SchoolGeofenceDto>> GetNearbySchoolsAsync(decimal lat, decimal lon, decimal radiusKm);
    Task<ContactDto> AddContactAsync(int schoolId, CreateContactRequest request);
    Task<ContactDto?> UpdateContactAsync(int contactId, UpdateContactRequest request);
    Task<bool> DeleteContactAsync(int contactId);
    Task<List<ContactListDto>> GetContactsBySchoolAsync(int schoolId);
}
