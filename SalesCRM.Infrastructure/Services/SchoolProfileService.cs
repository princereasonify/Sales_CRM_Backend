using System.Text;
using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.SchoolProfile;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class SchoolProfileService : ISchoolProfileService
{
    private readonly IUnitOfWork _uow;

    public SchoolProfileService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<SchoolProfileDto>> GetAllAsync()
    {
        return await _uow.SchoolProfiles.Query()
            .Include(p => p.CreatedBy)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => ToDto(p))
            .ToListAsync();
    }

    public async Task<SchoolProfileDto?> GetByIdAsync(int id)
    {
        var p = await _uow.SchoolProfiles.Query()
            .Include(p => p.CreatedBy)
            .FirstOrDefaultAsync(p => p.Id == id);
        return p == null ? null : ToDto(p);
    }

    public async Task<SchoolProfileDto> CreateAsync(CreateSchoolProfileRequest request, int createdById)
    {
        var profile = new SchoolProfile
        {
            SchoolId = request.SchoolId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserPhone = request.UserPhone,
            UserEmail = request.UserEmail,
            Password = request.Password,
            Gender = request.Gender,
            SchoolName = request.SchoolName,
            SchoolAddress = request.SchoolAddress,
            Area = request.Area,
            City = request.City,
            State = request.State,
            Country = request.Country,
            SchoolPhone = request.SchoolPhone,
            SchoolEmail = request.SchoolEmail,
            Zipcode = request.Zipcode,
            CreatedById = createdById
        };

        await _uow.SchoolProfiles.AddAsync(profile);
        await _uow.SaveChangesAsync();
        return (await GetByIdAsync(profile.Id))!;
    }

    public async Task<SchoolProfileDto?> UpdateAsync(int id, UpdateSchoolProfileRequest request)
    {
        var profile = await _uow.SchoolProfiles.GetByIdAsync(id);
        if (profile == null) return null;

        profile.FirstName = request.FirstName;
        profile.LastName = request.LastName;
        profile.UserPhone = request.UserPhone;
        profile.UserEmail = request.UserEmail;
        profile.Password = request.Password;
        profile.Gender = request.Gender;
        profile.SchoolName = request.SchoolName;
        profile.SchoolAddress = request.SchoolAddress;
        profile.Area = request.Area;
        profile.City = request.City;
        profile.State = request.State;
        profile.Country = request.Country;
        profile.SchoolPhone = request.SchoolPhone;
        profile.SchoolEmail = request.SchoolEmail;
        profile.Zipcode = request.Zipcode;

        await _uow.SchoolProfiles.UpdateAsync(profile);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<SchoolProfilePrefillDto> GetPrefillAsync(int schoolId)
    {
        var school = await _uow.Schools.GetByIdAsync(schoolId);
        var prefill = new SchoolProfilePrefillDto();

        if (school != null)
        {
            prefill.SchoolName = school.Name;
            prefill.SchoolAddress = school.Address ?? "";
            prefill.City = school.City ?? "";
            prefill.State = school.State ?? "";
            prefill.SchoolPhone = school.Phone ?? "";
            prefill.SchoolEmail = school.Email ?? "";
            prefill.Zipcode = school.Pincode ?? "";
        }

        // Try to get contact info from lead
        var schoolName = school?.Name ?? "";
        var lead = await _uow.Leads.Query()
            .Where(l => l.School == schoolName)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        if (lead != null)
        {
            var nameParts = lead.ContactName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            prefill.FirstName = nameParts.Length > 0 ? nameParts[0] : "";
            prefill.LastName = nameParts.Length > 1 ? nameParts[1] : "";
            prefill.UserPhone = lead.ContactPhone;
            prefill.UserEmail = lead.ContactEmail;
        }

        return prefill;
    }

    public async Task<List<OnboardedSchoolDto>> GetOnboardedSchoolsAsync()
    {
        // Get school names from leads that have Won stage
        var wonLeadSchoolNames = await _uow.Leads.Query()
            .Where(l => l.Stage == LeadStage.Won || l.Stage == LeadStage.ImplementationStarted)
            .Select(l => l.School)
            .Distinct()
            .ToListAsync();

        // Match to actual School entities
        var schools = await _uow.Schools.Query()
            .Where(s => wonLeadSchoolNames.Contains(s.Name))
            .OrderBy(s => s.Name)
            .Select(s => new OnboardedSchoolDto
            {
                Id = s.Id,
                Name = s.Name,
                City = s.City,
                State = s.State
            })
            .ToListAsync();

        return schools;
    }

    public async Task<string> ExportCsvAsync()
    {
        var profiles = await _uow.SchoolProfiles.Query()
            .Include(p => p.CreatedBy)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("First Name,Last Name,User Phone,User Email,Password,Gender,School Name,School Address,Area,City,State,Country,School Phone,School Email,Zipcode,Created By,Created At");

        foreach (var p in profiles)
        {
            sb.AppendLine($"{Csv(p.FirstName)},{Csv(p.LastName)},{Csv(p.UserPhone)},{Csv(p.UserEmail)},{Csv(p.Password)},{Csv(p.Gender)},{Csv(p.SchoolName)},{Csv(p.SchoolAddress)},{Csv(p.Area)},{Csv(p.City)},{Csv(p.State)},{Csv(p.Country)},{Csv(p.SchoolPhone)},{Csv(p.SchoolEmail)},{Csv(p.Zipcode)},{Csv(p.CreatedBy?.Name ?? "")},{p.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        return sb.ToString();
    }

    private static string Csv(string val) =>
        val.Contains(',') || val.Contains('"') || val.Contains('\n')
            ? $"\"{val.Replace("\"", "\"\"")}\""
            : val;

    private static SchoolProfileDto ToDto(SchoolProfile p) => new()
    {
        Id = p.Id,
        SchoolId = p.SchoolId,
        FirstName = p.FirstName,
        LastName = p.LastName,
        UserPhone = p.UserPhone,
        UserEmail = p.UserEmail,
        Password = p.Password,
        Gender = p.Gender,
        SchoolName = p.SchoolName,
        SchoolAddress = p.SchoolAddress,
        Area = p.Area,
        City = p.City,
        State = p.State,
        Country = p.Country,
        SchoolPhone = p.SchoolPhone,
        SchoolEmail = p.SchoolEmail,
        Zipcode = p.Zipcode,
        CreatedByName = p.CreatedBy?.Name ?? "",
        CreatedAt = p.CreatedAt
    };
}
