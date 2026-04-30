namespace SalesCRM.Core.Entities;

public class DemoRecording : BaseEntity
{
    public int UserId { get; set; }
    public string MediaType { get; set; } = "video"; // video, audio, screen
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string GcsObjectName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public int DurationSec { get; set; }

    // Optional: which demo (if any) this recording is currently attached to
    public int? AttachedDemoId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public DemoAssignment? AttachedDemo { get; set; }
}
