using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.DTOs.Demos;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class DemoRecordingService : IDemoRecordingService
{
    private readonly IUnitOfWork _uow;
    private readonly IGcpStorageService _storage;
    private readonly ILogger<DemoRecordingService> _logger;

    private const long MaxFileSize = 314_572_800; // 300 MB

    private static readonly Dictionary<string, string[]> AllowedTypes = new()
    {
        ["video"] = new[] { "video/mp4", "video/webm", "video/quicktime", "video/3gpp" },
        ["audio"] = new[] { "audio/mpeg", "audio/wav", "audio/ogg", "audio/mp4", "audio/webm", "audio/aac" },
        ["screen"] = new[] { "video/mp4", "video/webm", "video/quicktime" }
    };

    public DemoRecordingService(IUnitOfWork uow, IGcpStorageService storage, ILogger<DemoRecordingService> logger)
    {
        _uow = uow;
        _storage = storage;
        _logger = logger;
    }

    public async Task<DemoRecordingDto> UploadRecordingAsync(
        int userId,
        Stream content,
        string contentType,
        string fileName,
        long fileSize,
        string mediaType,
        string? title,
        int? durationSec)
    {
        if (content == null || fileSize == 0)
            throw new InvalidOperationException("No file provided.");

        if (!AllowedTypes.TryGetValue(mediaType, out var allowed))
            throw new InvalidOperationException("Invalid mediaType. Use: video, audio, screen.");

        if (!allowed.Contains(contentType))
            throw new InvalidOperationException($"Invalid file type: {contentType}");

        if (fileSize > MaxFileSize)
            throw new InvalidOperationException($"File too large. Max: {MaxFileSize / 1048576}MB");

        var folder = mediaType switch
        {
            "video"  => "videos",
            "audio"  => "audio",
            "screen" => "screen-recordings",
            _ => "other"
        };

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".webm";

        var objectName = $"SalesDemoFeedbacks/{folder}/{Guid.NewGuid()}{ext}";

        var result = await _storage.UploadFileAsync(objectName, content, contentType);

        if (!result.Success)
            throw new InvalidOperationException($"Upload failed: {result.Error}");

        var recording = new DemoRecording
        {
            UserId = userId,
            MediaType = mediaType,
            Title = string.IsNullOrWhiteSpace(title) ? $"{char.ToUpper(mediaType[0]) + mediaType[1..]} – {DateTime.UtcNow:dd MMM yyyy HH:mm}" : title!,
            Url = result.PublicUrl ?? "",
            GcsObjectName = objectName,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            DurationSec = durationSec ?? 0
        };

        await _uow.DemoRecordings.AddAsync(recording);
        await _uow.SaveChangesAsync();

        // Populate the User nav for DTO output (poster name + role).
        recording.User = await _uow.Users.GetByIdAsync(userId) ?? recording.User;
        return ToDto(recording);
    }

    public async Task<List<DemoRecordingDto>> GetVisibleRecordingsAsync(
        int viewerUserId,
        string? mediaType,
        DateTime? from,
        DateTime? to,
        int? userIdFilter,
        string? search)
    {
        var viewer = await _uow.Users.GetByIdAsync(viewerUserId);
        if (viewer == null) return new List<DemoRecordingDto>();

        var q = _uow.DemoRecordings.Query()
            .Include(r => r.User).ThenInclude(u => u.Zone)
            .AsQueryable();

        // Hierarchy visibility:
        //   FO  → own only
        //   ZH  → own + all FOs in same Zone
        //   RH  → own + everyone whose Region matches (direct or via Zone)
        //   SH  → all
        //   SCA → all (read-only)
        switch (viewer.Role)
        {
            case UserRole.FO:
                q = q.Where(r => r.UserId == viewerUserId);
                break;
            case UserRole.ZH:
                q = q.Where(r => r.UserId == viewerUserId
                    || (r.User.Role == UserRole.FO && r.User.ZoneId == viewer.ZoneId));
                break;
            case UserRole.RH:
                q = q.Where(r => r.UserId == viewerUserId
                    || r.User.RegionId == viewer.RegionId
                    || (r.User.Zone != null && r.User.Zone.RegionId == viewer.RegionId));
                break;
            case UserRole.SH:
            case UserRole.SCA:
            default:
                // No filter — sees all
                break;
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
            q = q.Where(r => r.MediaType == mediaType);
        if (userIdFilter.HasValue)
            q = q.Where(r => r.UserId == userIdFilter.Value);
        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
            q = q.Where(r => r.CreatedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc);
            q = q.Where(r => r.CreatedAt < toUtc);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(r => r.Title.ToLower().Contains(term)
                || r.User.Name.ToLower().Contains(term));
        }

        var rows = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<bool> DeleteRecordingAsync(int userId, int recordingId)
    {
        var rec = await _uow.DemoRecordings.GetByIdAsync(recordingId);
        if (rec == null) return false;
        if (rec.UserId != userId) throw new UnauthorizedAccessException("Cannot delete recording owned by another user.");

        // Delete from GCS first; if that fails we still remove the DB row to avoid orphaned listings.
        if (!string.IsNullOrEmpty(rec.GcsObjectName))
            await _storage.DeleteFileAsync(rec.GcsObjectName);

        await _uow.DemoRecordings.DeleteAsync(rec);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<DemoAssignmentDto?> AttachRecordingToDemoAsync(int userId, int demoId, int recordingId)
    {
        var rec = await _uow.DemoRecordings.GetByIdAsync(recordingId);
        if (rec == null) throw new InvalidOperationException("Recording not found.");
        if (rec.UserId != userId) throw new UnauthorizedAccessException("Cannot attach a recording owned by another user.");

        var demo = await _uow.DemoAssignments.Query()
            .Include(d => d.Lead).Include(d => d.School)
            .Include(d => d.RequestedBy).Include(d => d.AssignedTo).Include(d => d.ApprovedBy)
            .FirstOrDefaultAsync(d => d.Id == demoId);
        if (demo == null) throw new InvalidOperationException("Demo not found.");

        // Only assignee or requester can attach a recording to a demo.
        if (demo.AssignedToId != userId && demo.RequestedById != userId)
            throw new UnauthorizedAccessException("Only the assigned FO or requester can attach recordings.");

        switch (rec.MediaType)
        {
            case "video":  demo.FeedbackVideoUrl = rec.Url; break;
            case "audio":  demo.FeedbackAudioUrl = rec.Url; break;
            case "screen": demo.ScreenRecordingUrl = rec.Url; break;
            default: throw new InvalidOperationException($"Unknown media type: {rec.MediaType}");
        }

        rec.AttachedDemoId = demo.Id;

        await _uow.DemoAssignments.UpdateAsync(demo);
        await _uow.DemoRecordings.UpdateAsync(rec);
        await _uow.SaveChangesAsync();

        return new DemoAssignmentDto
        {
            Id = demo.Id, LeadId = demo.LeadId, LeadName = demo.Lead?.School,
            SchoolId = demo.SchoolId, SchoolName = demo.School?.Name ?? "",
            RequestedById = demo.RequestedById, RequestedByName = demo.RequestedBy?.Name ?? "",
            AssignedToId = demo.AssignedToId, AssignedToName = demo.AssignedTo?.Name ?? "",
            ApprovedById = demo.ApprovedById, ApprovedByName = demo.ApprovedBy?.Name,
            ScheduledDate = demo.ScheduledDate,
            ScheduledStartTime = demo.ScheduledStartTime.ToString(@"hh\:mm"),
            ScheduledEndTime = demo.ScheduledEndTime.ToString(@"hh\:mm"),
            DemoMode = demo.DemoMode, Status = demo.Status.ToString(),
            MeetingLink = demo.MeetingLink, Notes = demo.Notes, Feedback = demo.Feedback,
            FeedbackSentiment = demo.FeedbackSentiment, FeedbackAudioUrl = demo.FeedbackAudioUrl,
            FeedbackVideoUrl = demo.FeedbackVideoUrl, ScreenRecordingUrl = demo.ScreenRecordingUrl,
            Outcome = demo.Outcome?.ToString(), CompletedAt = demo.CompletedAt, CreatedAt = demo.CreatedAt
        };
    }

    public async Task<DemoRecordingDto?> UpdateRecordingTitleAsync(int userId, int recordingId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Title cannot be empty.");
        if (title.Length > 200)
            throw new InvalidOperationException("Title must be 200 characters or fewer.");

        var rec = await _uow.DemoRecordings.GetByIdAsync(recordingId);
        if (rec == null) return null;
        if (rec.UserId != userId) throw new UnauthorizedAccessException("Cannot rename a recording owned by another user.");

        rec.Title = title.Trim();
        await _uow.DemoRecordings.UpdateAsync(rec);
        await _uow.SaveChangesAsync();
        rec.User = await _uow.Users.GetByIdAsync(rec.UserId) ?? rec.User;
        return ToDto(rec);
    }

    private static DemoRecordingDto ToDto(DemoRecording r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        UserName = r.User?.Name ?? string.Empty,
        UserRole = r.User?.Role.ToString() ?? string.Empty,
        MediaType = r.MediaType,
        Title = r.Title,
        Url = r.Url,
        ContentType = r.ContentType,
        FileSizeBytes = r.FileSizeBytes,
        DurationSec = r.DurationSec,
        AttachedDemoId = r.AttachedDemoId,
        CreatedAt = r.CreatedAt
    };
}
