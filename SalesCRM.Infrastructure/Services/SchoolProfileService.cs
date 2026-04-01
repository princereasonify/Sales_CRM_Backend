using System.Net;
using System.Net.Mail;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.DTOs.SchoolProfile;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class SchoolProfileService : ISchoolProfileService
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly ILogger<SchoolProfileService> _logger;

    public SchoolProfileService(IUnitOfWork uow, IConfiguration config, ILogger<SchoolProfileService> logger)
    {
        _uow = uow;
        _config = config;
        _logger = logger;
    }

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

    public async Task<SchoolProfileDto> CreateAsync(CreateSchoolProfileRequest request, int createdById, string exportFormat)
    {
        var (foName, foEmail) = await GetFoDetailsForSchoolAsync(request.SchoolId);

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
            SchoolLogo = request.SchoolLogo,
            FoName = foName,
            FoEmail = foEmail,
            CreatedById = createdById
        };

        await _uow.SchoolProfiles.AddAsync(profile);
        await _uow.SaveChangesAsync();

        var dto = (await GetByIdAsync(profile.Id))!;

        await SendProfileEmailAsync(dto, exportFormat);

        return dto;
    }

    public async Task<SchoolProfileDto?> UpdateAsync(int id, UpdateSchoolProfileRequest request, string exportFormat)
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
        profile.SchoolLogo = request.SchoolLogo;

        // Refresh FO details if missing
        if (string.IsNullOrEmpty(profile.FoEmail))
        {
            var (foName, foEmail) = await GetFoDetailsForSchoolAsync(profile.SchoolId);
            if (!string.IsNullOrEmpty(foEmail)) profile.FoEmail = foEmail;
            if (!string.IsNullOrEmpty(foName) && string.IsNullOrEmpty(profile.FoName)) profile.FoName = foName;
        }

        await _uow.SchoolProfiles.UpdateAsync(profile);
        await _uow.SaveChangesAsync();

        var dto = (await GetByIdAsync(id))!;

        await SendProfileEmailAsync(dto, exportFormat);

        return dto;
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

        var schoolName = school?.Name ?? "";
        var lead = await _uow.Leads.Query()
            .Include(l => l.Fo)
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
            prefill.FoName = lead.Fo?.Name ?? "";
        }

        return prefill;
    }

    public async Task<List<OnboardedSchoolDto>> GetOnboardedSchoolsAsync()
    {
        var wonLeadSchoolNames = await _uow.Leads.Query()
            .Where(l => l.Stage == LeadStage.Won || l.Stage == LeadStage.ImplementationStarted)
            .Select(l => l.School)
            .Distinct()
            .ToListAsync();

        return await _uow.Schools.Query()
            .Where(s => wonLeadSchoolNames.Contains(s.Name))
            .OrderBy(s => s.Name)
            .Select(s => new OnboardedSchoolDto { Id = s.Id, Name = s.Name, City = s.City, State = s.State })
            .ToListAsync();
    }

    public async Task<string> ExportCsvAsync()
    {
        var profiles = await _uow.SchoolProfiles.Query()
            .Include(p => p.CreatedBy)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Seq,firstName,lastName,userPhone,userEmail,password,gender,schoolName,schoolAddress,area,city,state,country,schoolPhone,schoolEmail,zipcode,logo,foName,FOEmail");

        foreach (var p in profiles)
        {
            sb.AppendLine($"{p.Id},{Csv(p.FirstName)},{Csv(p.LastName)},{Csv(p.UserPhone)},{Csv(p.UserEmail)},{Csv(p.Password)},{Csv(p.Gender)},{Csv(p.SchoolName)},{Csv(p.SchoolAddress)},{Csv(p.Area)},{Csv(p.City)},{Csv(p.State)},{Csv(p.Country)},{Csv(p.SchoolPhone)},{Csv(p.SchoolEmail)},{Csv(p.Zipcode)},{Csv(p.SchoolLogo ?? "")},{Csv(p.FoName)},{Csv(p.FoEmail)}");
        }

        return sb.ToString();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<(string Name, string Email)> GetFoDetailsForSchoolAsync(int schoolId)
    {
        var school = await _uow.Schools.GetByIdAsync(schoolId);
        if (school == null) return ("", "");

        var lead = await _uow.Leads.Query()
            .Include(l => l.Fo)
            .Where(l => l.School == school.Name && (l.Stage == LeadStage.Won || l.Stage == LeadStage.ImplementationStarted))
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        return (lead?.Fo?.Name ?? "", lead?.Fo?.Email ?? "");
    }

    private static void SetTextCell(IXLCell cell, string value)
    {
        cell.Style.NumberFormat.Format = "@";
        cell.SetValue(value);
    }

    private byte[] GenerateExcel(SchoolProfileDto p)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("SchoolProfile");

        var headers = new[] { "Seq", "firstName", "lastName", "userPhone", "userEmail", "password", "gender",
            "schoolName", "schoolAddress", "area", "city", "state", "country", "schoolPhone", "schoolEmail", "zipcode", "logo", "foName", "FOEmail" };

        // Style header row
        for (int i = 0; i < headers.Length; i++)
        {
            var hCell = ws.Cell(1, i + 1);
            hCell.Value = headers[i];
            hCell.Style.Font.Bold = true;
            hCell.Style.Fill.BackgroundColor = XLColor.LightGray;
            hCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data row — phone/zipcode columns set as text BEFORE value
        ws.Cell(2, 1).Value = p.Id;
        SetTextCell(ws.Cell(2, 2), p.FirstName);
        SetTextCell(ws.Cell(2, 3), p.LastName);
        SetTextCell(ws.Cell(2, 4), p.UserPhone);
        SetTextCell(ws.Cell(2, 5), p.UserEmail);
        SetTextCell(ws.Cell(2, 6), p.Password);
        SetTextCell(ws.Cell(2, 7), p.Gender);
        SetTextCell(ws.Cell(2, 8), p.SchoolName);
        SetTextCell(ws.Cell(2, 9), p.SchoolAddress);
        SetTextCell(ws.Cell(2, 10), p.Area);
        SetTextCell(ws.Cell(2, 11), p.City);
        SetTextCell(ws.Cell(2, 12), p.State);
        SetTextCell(ws.Cell(2, 13), p.Country);
        SetTextCell(ws.Cell(2, 14), p.SchoolPhone);
        SetTextCell(ws.Cell(2, 15), p.SchoolEmail);
        SetTextCell(ws.Cell(2, 16), p.Zipcode);
        SetTextCell(ws.Cell(2, 17), p.SchoolLogo ?? "");
        SetTextCell(ws.Cell(2, 18), p.FoName);
        SetTextCell(ws.Cell(2, 19), p.FoEmail);

        // Auto-fit with minimum width so nothing gets cut off
        ws.Columns().AdjustToContents();
        foreach (var col in ws.ColumnsUsed())
        {
            if (col.Width < 18) col.Width = 18;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] GenerateCsvBytes(SchoolProfileDto p)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Seq,firstName,lastName,userPhone,userEmail,password,gender,schoolName,schoolAddress,area,city,state,country,schoolPhone,schoolEmail,zipcode,logo,foName,FOEmail");
        sb.AppendLine($"{p.Id},{CsvVal(p.FirstName)},{CsvVal(p.LastName)},{CsvVal(p.UserPhone)},{CsvVal(p.UserEmail)},{CsvVal(p.Password)},{CsvVal(p.Gender)},{CsvVal(p.SchoolName)},{CsvVal(p.SchoolAddress)},{CsvVal(p.Area)},{CsvVal(p.City)},{CsvVal(p.State)},{CsvVal(p.Country)},{CsvVal(p.SchoolPhone)},{CsvVal(p.SchoolEmail)},{CsvVal(p.Zipcode)},{CsvVal(p.SchoolLogo ?? "")},{CsvVal(p.FoName)},{CsvVal(p.FoEmail)}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task SendProfileEmailAsync(SchoolProfileDto profile, string format)
    {
        var smtpHost = _config["Smtp:Host"];
        var smtpPort = int.TryParse(_config["Smtp:Port"], out var port) ? port : 587;
        var smtpUser = _config["Smtp:Username"];
        var smtpPass = _config["Smtp:Password"];
        var fromEmail = _config["Smtp:FromEmail"];
        var fromName = _config["Smtp:FromName"] ?? "SalesCRM";
        var toEmail = _config["Smtp:ToEmail"];

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(toEmail) ||
            string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
        {
            _logger.LogWarning("SMTP not configured. Skipping school profile email.");
            return;
        }

        byte[] fileBytes;
        string fileName;
        string contentType;

        if (format == "excel")
        {
            fileBytes = GenerateExcel(profile);
            fileName = $"{profile.SchoolName}_Profile.xlsx";
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }
        else
        {
            fileBytes = GenerateCsvBytes(profile);
            fileName = $"{profile.SchoolName}_Profile.csv";
            contentType = "text/csv";
        }

        using var msg = new MailMessage();
        msg.From = new MailAddress(fromEmail!, fromName);
        msg.To.Add(toEmail);
        msg.Subject = $"School Profile - {profile.SchoolName}";
        msg.Body = $"School profile for {profile.SchoolName} has been saved.\n\nAdmin: {profile.FirstName} {profile.LastName}\nFO: {profile.FoName}\nCity: {profile.City}, {profile.State}\n\nPlease find the {(format == "excel" ? "Excel" : "CSV")} attachment.";
        msg.IsBodyHtml = false;

        msg.Attachments.Add(new Attachment(new MemoryStream(fileBytes), fileName, contentType));

        using var smtp = new SmtpClient(smtpHost, smtpPort);
        smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);
        smtp.EnableSsl = true;

        await smtp.SendMailAsync(msg);
        _logger.LogInformation("School profile email sent for {SchoolName} to {ToEmail} as {Format}", profile.SchoolName, toEmail, format);
    }

    private static string Csv(string val) =>
        $"\"{val.Replace("\"", "\"\"")}\"";

    private static string CsvVal(string val) =>
        $"\"{val.Replace("\"", "\"\"")}\"";

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
        SchoolLogo = p.SchoolLogo,
        FoName = p.FoName,
        FoEmail = p.FoEmail,
        CreatedByName = p.CreatedBy?.Name ?? "",
        CreatedAt = p.CreatedAt
    };
}
