using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Services;

/// <summary>
/// Background service that runs at midnight IST (18:30 UTC) to auto-close stale active sessions.
/// </summary>
public class MidnightResetService : IHostedService, IDisposable
{
    private Timer? _timer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MidnightResetService> _logger;

    public MidnightResetService(IServiceScopeFactory scopeFactory, ILogger<MidnightResetService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MidnightResetService started.");
        // Check every hour
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        try
        {
            // Check if it's around midnight IST (18:30 UTC)
            var utcNow = DateTime.UtcNow;
            var ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, ist);

            // Run between 00:00 and 01:00 IST
            if (istNow.Hour == 0)
            {
                _logger.LogInformation("Running midnight IST reset for stale tracking sessions...");
                using var scope = _scopeFactory.CreateScope();
                var trackingService = scope.ServiceProvider.GetRequiredService<ITrackingService>();
                await trackingService.CloseStaleSessionsAsync();
                _logger.LogInformation("Midnight reset completed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MidnightResetService.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();
}
