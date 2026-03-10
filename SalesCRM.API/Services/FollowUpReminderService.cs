using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Services;

public class FollowUpReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FollowUpReminderService> _logger;

    public FollowUpReminderService(IServiceProvider serviceProvider, ILogger<FollowUpReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.CreateFollowUpRemindersAsync();
                _logger.LogInformation("Follow-up reminders checked at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating follow-up reminders");
            }

            // Run every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
