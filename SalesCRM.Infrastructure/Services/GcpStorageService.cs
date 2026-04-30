using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.Interfaces;
using System.Text;

namespace SalesCRM.Infrastructure.Services;

public class GcpStorageService : IGcpStorageService
{
    private readonly StorageClient _client;
    private readonly string _bucketName;
    private readonly ILogger<GcpStorageService> _logger;

    public GcpStorageService(IConfiguration configuration, ILogger<GcpStorageService> logger)
    {
        _logger = logger;
        _bucketName = configuration["Gcp:BucketName"]!;

        GoogleCredential? credential = null;

        // 1) Credentials file path
        var credentialsPath = configuration["Gcp:CredentialsPath"]
            ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(credentialsPath) && File.Exists(credentialsPath))
        {
            using var stream = File.OpenRead(credentialsPath);
            credential = GoogleCredential.FromStream(stream);
            _logger.LogInformation("GCP Storage: using credentials from file {Path}", credentialsPath);
        }
        // 2) Credentials JSON string
        else
        {
            var credentialsJson = configuration["Gcp:CredentialsJson"]
                ?? Environment.GetEnvironmentVariable("GCP_CREDENTIALS_JSON");
            if (!string.IsNullOrWhiteSpace(credentialsJson))
            {
                var bytes = Encoding.UTF8.GetBytes(credentialsJson);
                using var stream = new MemoryStream(bytes);
                credential = GoogleCredential.FromStream(stream);
                _logger.LogInformation("GCP Storage: using credentials from config JSON");
            }
        }

        _client = credential != null
            ? StorageClient.Create(credential)
            : StorageClient.Create();
    }

    public async Task<GcpUploadResult> UploadFileAsync(string objectName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.UploadObjectAsync(
                _bucketName,
                objectName,
                contentType,
                content,
                cancellationToken: cancellationToken);

            var publicUrl = $"https://storage.googleapis.com/{_bucketName}/{objectName}";
            _logger.LogInformation("Uploaded to GCS: {ObjectName}", objectName);

            return new GcpUploadResult
            {
                Success = true,
                GcsPath = objectName,
                PublicUrl = publicUrl,
                ContentType = contentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload to GCS: {ObjectName}", objectName);
            return new GcpUploadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<bool> DeleteFileAsync(string objectName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucketName, objectName, cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted from GCS: {ObjectName}", objectName);
            return true;
        }
        catch (Google.GoogleApiException gex) when (gex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone — treat as success.
            _logger.LogWarning("GCS object not found on delete (treating as success): {ObjectName}", objectName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete from GCS: {ObjectName}", objectName);
            return false;
        }
    }
}
