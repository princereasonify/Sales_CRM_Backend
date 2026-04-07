using Microsoft.Extensions.Options;
using SalesCRM.Core.Interfaces;
using SalesCRM.Core.Options;

namespace SalesCRM.API.Services;

public class AiReportGenerationService : IHostedService, IDisposable
{
    private Timer? _timer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiReportGenerationService> _logger;
    private readonly int _foDailyHour;
    private readonly int _mgmtHour;
    private readonly DayOfWeek _mgmtDayOfWeek;

    public AiReportGenerationService(
        IServiceScopeFactory scopeFactory,
        ILogger<AiReportGenerationService> logger,
        IOptions<AiReportOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var cfg = options.Value;
        _foDailyHour = TimeOnly.Parse(cfg.FoDailyReportTimeIst).Hour;
        _mgmtHour = TimeOnly.Parse(cfg.ManagementReportTimeIst).Hour;
        _mgmtDayOfWeek = Enum.Parse<DayOfWeek>(cfg.ManagementReportDayOfWeek, ignoreCase: true);

        _logger.LogInformation(
            "AiReportGenerationService configured: FO daily at {FoHour}:00 IST, Management weekly on {Day} at {MgmtHour}:00 IST",
            _foDailyHour, _mgmtDayOfWeek, _mgmtHour);
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

            // FO Daily Reports
            if (istNow.Hour == _foDailyHour)
            {
                _logger.LogInformation("Triggering FO daily report generation for {Date}", istNow.Date);
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IAiReportService>();
                await svc.GenerateAllFoDailyReportsAsync(istNow.Date);
                _logger.LogInformation("FO daily reports generation completed.");
            }

            // FO Weekly Reports (Saturday)
            if (istNow.Hour == _mgmtHour && istNow.DayOfWeek == _mgmtDayOfWeek)
            {
                var weekEnd = istNow.Date;
                var weekStart = weekEnd.AddDays(-6);

                _logger.LogInformation("Triggering FO weekly + management reports for {Start} to {End}", weekStart, weekEnd);
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IAiReportService>();
                await svc.GenerateAllFoWeeklyReportsAsync(weekStart, weekEnd);
                await svc.GenerateAllManagementReportsAsync(weekStart, weekEnd);
                _logger.LogInformation("Weekly reports generation completed.");
            }

            // FO Monthly Reports (1st of month at FO daily report time)
            if (istNow.Hour == _foDailyHour && istNow.Day == 1)
            {
                var monthEnd = istNow.Date.AddDays(-1); // last day of previous month
                var monthStart = new DateTime(monthEnd.Year, monthEnd.Month, 1);

                _logger.LogInformation("Triggering FO monthly reports for {Month}", monthStart.ToString("MMM yyyy"));
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IAiReportService>();
                await svc.GenerateAllFoMonthlyReportsAsync(monthStart, monthEnd);
                _logger.LogInformation("Monthly reports generation completed.");
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
