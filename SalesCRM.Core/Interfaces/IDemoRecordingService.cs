using SalesCRM.Core.DTOs.Demos;

namespace SalesCRM.Core.Interfaces;

public interface IDemoRecordingService
{
    Task<DemoRecordingDto> UploadRecordingAsync(
        int userId,
        Stream content,
        string contentType,
        string fileName,
        long fileSize,
        string mediaType,
        string? title,
        int? durationSec);

    Task<List<DemoRecordingDto>> GetVisibleRecordingsAsync(
        int viewerUserId,
        string? mediaType,
        DateTime? from,
        DateTime? to,
        int? userIdFilter,
        string? search);

    Task<bool> DeleteRecordingAsync(int userId, int recordingId);
    Task<DemoAssignmentDto?> AttachRecordingToDemoAsync(int userId, int demoId, int recordingId);
    Task<DemoRecordingDto?> UpdateRecordingTitleAsync(int userId, int recordingId, string title);
}
