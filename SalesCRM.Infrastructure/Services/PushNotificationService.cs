using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class PushNotificationService : IPushNotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly string _projectId;
    private readonly GoogleCredential _credential;
    private readonly HttpClient _httpClient;

    public PushNotificationService(IConfiguration configuration, ILogger<PushNotificationService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _httpClient = new HttpClient();

        _projectId = configuration["Firebase:ProjectId"] ?? "";

        if (string.IsNullOrEmpty(_projectId))
        {
            _logger.LogWarning("Firebase:ProjectId not configured — push notifications disabled");
            _credential = null!;
            return;
        }

        // Reuse the same GCP credentials (config.json), scoped for FCM
        var credentialsPath = configuration["Gcp:CredentialsPath"] ?? configuration["Gcp__CredentialsPath"]
            ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

        GoogleCredential? baseCred = null;

        if (!string.IsNullOrWhiteSpace(credentialsPath) && File.Exists(credentialsPath))
        {
            using var stream = File.OpenRead(credentialsPath);
            baseCred = GoogleCredential.FromStream(stream);
        }
        else
        {
            var credentialsJson = configuration["Gcp:CredentialsJson"] ?? configuration["Gcp__CredentialsJson"]
                ?? Environment.GetEnvironmentVariable("GCP_CREDENTIALS_JSON");
            if (!string.IsNullOrWhiteSpace(credentialsJson))
            {
                var bytes = Encoding.UTF8.GetBytes(credentialsJson);
                using var stream = new MemoryStream(bytes);
                baseCred = GoogleCredential.FromStream(stream);
            }
            else
            {
                baseCred = GoogleCredential.GetApplicationDefault();
            }
        }

        _credential = baseCred.CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        _logger.LogInformation("PushNotificationService initialized for project {ProjectId}", _projectId);
    }

    public async Task SendPushAsync(int userId, string title, string body, string type = "Info")
    {
        if (string.IsNullOrEmpty(_projectId) || _credential == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var token = await uow.Users.Query()
                .Where(u => u.Id == userId && u.FcmToken != null && u.FcmToken != "")
                .Select(u => u.FcmToken)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(token)) return;

            await SendMessageAsync(token, title, body, new Dictionary<string, string> { { "type", type } });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push failed for user {UserId}", userId);
        }
    }

    public async Task SendPushToRoleAsync(string role, string title, string body, string type = "Info", int? zoneId = null, int? regionId = null)
    {
        if (string.IsNullOrEmpty(_projectId) || _credential == null) return;

        try
        {
            if (!Enum.TryParse<UserRole>(role, out var userRole)) return;

            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var query = uow.Users.Query()
                .Where(u => u.Role == userRole && u.FcmToken != null && u.FcmToken != "" && u.IsActive);

            if (zoneId.HasValue) query = query.Where(u => u.ZoneId == zoneId);
            if (regionId.HasValue) query = query.Where(u => u.RegionId == regionId);

            var tokens = await query.Select(u => u.FcmToken!).ToListAsync();

            foreach (var token in tokens)
            {
                await SendMessageAsync(token, title, body, new Dictionary<string, string> { { "type", type } });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push to role {Role} failed", role);
        }
    }

    private async Task SendMessageAsync(string token, string title, string body, Dictionary<string, string>? data)
    {
        try
        {
            var accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            var url = $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send";

            var message = new
            {
                message = new
                {
                    token,
                    notification = new { title, body },
                    data
                }
            };

            var json = JsonSerializer.Serialize(message);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("FCM send failed: {Status} {Error}", response.StatusCode, errorBody);

                // Clear invalid token
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    errorBody.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase))
                {
                    await ClearTokenAsync(token);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM send error");
        }
    }

    private async Task ClearTokenAsync(string token)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var user = await uow.Users.Query().FirstOrDefaultAsync(u => u.FcmToken == token);
            if (user != null)
            {
                user.FcmToken = null;
                await uow.SaveChangesAsync();
            }
        }
        catch { }
    }
}
