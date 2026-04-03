using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Contacts;
using SalesCRM.Core.DTOs.Schools;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class SchoolService : ISchoolService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notify;

    public SchoolService(IUnitOfWork uow, INotificationService notify)
    {
        _uow = uow;
        _notify = notify;
    }

    private static decimal HaversineKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double R = 6371.0;
        double dLat = ToRad((double)(lat2 - lat1));
        double dLon = ToRad((double)(lon2 - lon1));
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad((double)lat1)) * Math.Cos(ToRad((double)lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (decimal)(R * c);
    }
    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    public async Task<(List<SchoolListDto> Schools, int Total)> GetSchoolsAsync(
        int page, int limit, string? search, string? city, string? state, string? board)
    {
        var query = _uow.Schools.Query().Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term) || (s.City != null && s.City.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(s => s.City != null && s.City.ToLower() == city.ToLower());
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(s => s.State != null && s.State.ToLower() == state.ToLower());
        if (!string.IsNullOrWhiteSpace(board))
            query = query.Where(s => s.Board != null && s.Board.ToLower() == board.ToLower());

        var total = await query.CountAsync();

        var schools = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(s => new SchoolListDto
            {
                Id = s.Id,
                Name = s.Name,
                City = s.City,
                State = s.State,
                Board = s.Board,
                Type = s.Type,
                StudentCount = s.StudentCount,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                GeofenceRadiusMetres = s.GeofenceRadiusMetres,
                IsActive = s.IsActive,
                ContactCount = s.Contacts.Count(c => c.IsActive)
            })
            .ToListAsync();

        return (schools, total);
    }

    public async Task<SchoolDto?> GetSchoolByIdAsync(int id)
    {
        var s = await _uow.Schools.Query()
            .Include(x => x.Contacts.Where(c => c.IsActive))
            .FirstOrDefaultAsync(x => x.Id == id);

        if (s == null) return null;

        return new SchoolDto
        {
            Id = s.Id,
            Name = s.Name,
            Address = s.Address,
            City = s.City,
            State = s.State,
            Pincode = s.Pincode,
            Board = s.Board,
            Type = s.Type,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            GeofenceRadiusMetres = s.GeofenceRadiusMetres,
            StudentCount = s.StudentCount,
            StaffCount = s.StaffCount,
            Phone = s.Phone,
            Email = s.Email,
            Website = s.Website,
            PrincipalName = s.PrincipalName,
            PrincipalPhone = s.PrincipalPhone,
            IsActive = s.IsActive,
            ContactCount = s.Contacts.Count,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };
    }

    public async Task<SchoolDto> CreateSchoolAsync(CreateSchoolRequest request)
    {
        var school = new School
        {
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
            Board = request.Board,
            Type = request.Type,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            GeofenceRadiusMetres = request.GeofenceRadiusMetres > 0 ? request.GeofenceRadiusMetres : 100,
            StudentCount = request.StudentCount,
            StaffCount = request.StaffCount,
            Phone = request.Phone,
            Email = request.Email,
            Website = request.Website,
            PrincipalName = request.PrincipalName,
            PrincipalPhone = request.PrincipalPhone,
        };

        await _uow.Schools.AddAsync(school);
        await _uow.SaveChangesAsync();

        return (await GetSchoolByIdAsync(school.Id))!;
    }

    public async Task<SchoolDto?> UpdateSchoolAsync(int id, UpdateSchoolRequest request)
    {
        var school = await _uow.Schools.GetByIdAsync(id);
        if (school == null) return null;

        if (request.Name != null) school.Name = request.Name;
        if (request.Address != null) school.Address = request.Address;
        if (request.City != null) school.City = request.City;
        if (request.State != null) school.State = request.State;
        if (request.Pincode != null) school.Pincode = request.Pincode;
        if (request.Board != null) school.Board = request.Board;
        if (request.Type != null) school.Type = request.Type;
        if (request.Latitude.HasValue) school.Latitude = request.Latitude.Value;
        if (request.Longitude.HasValue) school.Longitude = request.Longitude.Value;
        if (request.GeofenceRadiusMetres.HasValue) school.GeofenceRadiusMetres = request.GeofenceRadiusMetres.Value;
        if (request.StudentCount.HasValue) school.StudentCount = request.StudentCount;
        if (request.StaffCount.HasValue) school.StaffCount = request.StaffCount;
        if (request.Phone != null) school.Phone = request.Phone;
        if (request.Email != null) school.Email = request.Email;
        if (request.Website != null) school.Website = request.Website;
        if (request.PrincipalName != null) school.PrincipalName = request.PrincipalName;
        if (request.PrincipalPhone != null) school.PrincipalPhone = request.PrincipalPhone;
        if (request.IsActive.HasValue) school.IsActive = request.IsActive.Value;

        await _uow.SaveChangesAsync();
        return await GetSchoolByIdAsync(id);
    }

    public async Task<bool> DeleteSchoolAsync(int id)
    {
        var school = await _uow.Schools.GetByIdAsync(id);
        if (school == null) return false;

        school.IsActive = false;
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<List<SchoolGeofenceDto>> GetSchoolsForMapAsync()
    {
        return await _uow.Schools.Query()
            .Where(s => s.IsActive && s.Latitude != 0 && s.Longitude != 0)
            .Select(s => new SchoolGeofenceDto
            {
                Id = s.Id,
                Name = s.Name,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                GeofenceRadiusMetres = s.GeofenceRadiusMetres
            })
            .ToListAsync();
    }

    public async Task<List<SchoolGeofenceDto>> GetNearbySchoolsAsync(decimal lat, decimal lon, decimal radiusKm)
    {
        // Bounding box pre-filter (rough, fast) then Haversine post-filter (accurate)
        var degOffset = radiusKm / 111m; // ~111 km per degree latitude
        var allInBox = await _uow.Schools.Query()
            .Where(s => s.IsActive &&
                        s.Latitude >= lat - degOffset && s.Latitude <= lat + degOffset &&
                        s.Longitude >= lon - degOffset && s.Longitude <= lon + degOffset)
            .ToListAsync();

        return allInBox
            .Where(s => HaversineKm(lat, lon, s.Latitude, s.Longitude) <= radiusKm)
            .Select(s => new SchoolGeofenceDto
            {
                Id = s.Id,
                Name = s.Name,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                GeofenceRadiusMetres = s.GeofenceRadiusMetres
            })
            .ToList();
    }

    // ─── Contacts ─────────────────────────────────────────────────────────────

    public async Task<ContactDto> AddContactAsync(int schoolId, CreateContactRequest request)
    {
        var school = await _uow.Schools.GetByIdAsync(schoolId);
        if (school == null) throw new InvalidOperationException("School not found");

        var contact = new Contact
        {
            SchoolId = schoolId,
            Name = request.Name,
            Designation = request.Designation,
            Department = request.Department,
            Phone = request.Phone,
            AltPhone = request.AltPhone,
            Email = request.Email,
            Profession = request.Profession,
            PersonalityNotes = request.PersonalityNotes,
            IsDecisionMaker = request.IsDecisionMaker,
        };

        await _uow.Contacts.AddAsync(contact);
        await _uow.SaveChangesAsync();

        return new ContactDto
        {
            Id = contact.Id,
            Name = contact.Name,
            SchoolId = contact.SchoolId,
            SchoolName = school.Name,
            Designation = contact.Designation,
            Department = contact.Department,
            Phone = contact.Phone,
            AltPhone = contact.AltPhone,
            Email = contact.Email,
            Profession = contact.Profession,
            PersonalityNotes = contact.PersonalityNotes,
            IsDecisionMaker = contact.IsDecisionMaker,
            IsActive = contact.IsActive,
            CreatedAt = contact.CreatedAt
        };
    }

    public async Task<ContactDto?> UpdateContactAsync(int contactId, UpdateContactRequest request)
    {
        var contact = await _uow.Contacts.Query()
            .Include(c => c.School)
            .FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null) return null;

        if (request.Name != null) contact.Name = request.Name;
        if (request.Designation != null) contact.Designation = request.Designation;
        if (request.Department != null) contact.Department = request.Department;
        if (request.Phone != null) contact.Phone = request.Phone;
        if (request.AltPhone != null) contact.AltPhone = request.AltPhone;
        if (request.Email != null) contact.Email = request.Email;
        if (request.Profession != null) contact.Profession = request.Profession;
        if (request.PersonalityNotes != null) contact.PersonalityNotes = request.PersonalityNotes;
        if (request.IsDecisionMaker.HasValue) contact.IsDecisionMaker = request.IsDecisionMaker.Value;
        if (request.IsActive.HasValue) contact.IsActive = request.IsActive.Value;

        await _uow.SaveChangesAsync();

        return new ContactDto
        {
            Id = contact.Id,
            Name = contact.Name,
            SchoolId = contact.SchoolId,
            SchoolName = contact.School?.Name,
            Designation = contact.Designation,
            Department = contact.Department,
            Phone = contact.Phone,
            AltPhone = contact.AltPhone,
            Email = contact.Email,
            Profession = contact.Profession,
            PersonalityNotes = contact.PersonalityNotes,
            IsDecisionMaker = contact.IsDecisionMaker,
            IsActive = contact.IsActive,
            CreatedAt = contact.CreatedAt
        };
    }

    public async Task<bool> DeleteContactAsync(int contactId)
    {
        var contact = await _uow.Contacts.GetByIdAsync(contactId);
        if (contact == null) return false;

        contact.IsActive = false;
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<List<ContactListDto>> GetContactsBySchoolAsync(int schoolId)
    {
        return await _uow.Contacts.Query()
            .Include(c => c.School)
            .Where(c => c.SchoolId == schoolId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ContactListDto
            {
                Id = c.Id,
                Name = c.Name,
                Designation = c.Designation,
                Phone = c.Phone,
                Email = c.Email,
                SchoolId = c.SchoolId,
                SchoolName = c.School.Name,
                IsDecisionMaker = c.IsDecisionMaker
            })
            .ToListAsync();
    }
}
