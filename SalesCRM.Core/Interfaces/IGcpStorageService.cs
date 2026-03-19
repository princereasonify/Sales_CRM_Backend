namespace SalesCRM.Core.Interfaces;

public class GcpUploadResult
{
    public bool Success { get; set; }
    public string? GcsPath { get; set; }
    public string? PublicUrl { get; set; }
    public string? ContentType { get; set; }
    public string? Error { get; set; }
}

public interface IGcpStorageService
{
    Task<GcpUploadResult> UploadFileAsync(string objectName, Stream content, string contentType, CancellationToken cancellationToken = default);
}
