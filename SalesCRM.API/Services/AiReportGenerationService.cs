using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Services;

public class AiReportGenerationService : IHostedService, IDisposable
{
    private Timer? _timer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiReportGenerationService> _logger;

    public AiReportGenerationService(IServiceScopeFactory scopeFactory, ILogger<AiReportGenerationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AiReportGenerationService started.");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        try
        {
            var utcNow = DateTime.UtcNow;
            var ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, ist);

            // FO Daily Reports — 23:00 IST
            if (istNow.Hour == 23)
            {
                _logger.LogInformation("Triggering FO daily report generation for {Date}", istNow.Date);
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IAiReportService>();
                await svc.GenerateAllFoDailyReportsAsync(istNow.Date);
                _logger.LogInformation("FO daily reports generation completed.");
            }

            // Management Bi-Weekly Reports — 06:00 IST on 1st and 16th
            if (istNow.Hour == 6 && (istNow.Day == 1 || istNow.Day == 16))
            {
                DateTime periodStart, periodEnd;
                if (istNow.Day == 1)
                {
                    // Report for 16th to end of previous month
                    var prevMonth = istNow.AddMonths(-1);
                    periodStart = new DateTime(prevMonth.Year, prevMonth.Month, 16);
                    periodEnd = new DateTime(prevMonth.Year, prevMonth.Month, DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month));
                }
                else
                {
                    // Report for 1st to 15th of current month
                    periodStart = new DateTime(istNow.Year, istNow.Month, 1);
                    periodEnd = new DateTime(istNow.Year, istNow.Month, 15);
                }

                _logger.LogInformation("Triggering management report generation for {Start} to {End}", periodStart, periodEnd);
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IAiReportService>();
                await svc.GenerateAllManagementReportsAsync(periodStart, periodEnd);
                _logger.LogInformation("Management reports generation completed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AiReportGenerationService.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();
}
