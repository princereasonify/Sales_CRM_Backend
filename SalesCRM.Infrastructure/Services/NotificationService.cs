using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationService? _push;

    public NotificationService(IUnitOfWork unitOfWork, IPushNotificationService? push = null)
    {
        _unitOfWork = unitOfWork;
        _push = push;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(int userId)
    {
        return await _unitOfWork.Notifications.Query()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type.ToString(),
                Title = n.Title,
                Body = n.Body,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _unitOfWork.Notifications.Query()
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification != null)
        {
            notification.IsRead = true;
            await _unitOfWork.Notifications.UpdateAsync(notification);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unread = await _unitOfWork.Notifications.Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            await _unitOfWork.Notifications.UpdateAsync(n);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CreateNotificationAsync(int userId, NotificationType type, string title, string body)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            IsRead = false
        };

        await _unitOfWork.Notifications.AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();

        // Send Firebase push notification
        if (_push != null)
        {
            try { await _push.SendPushAsync(userId, title, body, type.ToString()); }
            catch { /* push is best-effort */ }
        }
    }

    public async Task CreateFollowUpRemindersAsync()
    {
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        var dayAfter = tomorrow.AddDays(1);

        // Find activities with follow-up dates that are tomorrow
        var upcomingFollowUps = await _unitOfWork.Activities.Query()
            .Include(a => a.Lead)
            .Where(a => a.NextFollowUpDate.HasValue
                && a.NextFollowUpDate.Value.Date >= tomorrow
                && a.NextFollowUpDate.Value.Date < dayAfter)
            .ToListAsync();

        foreach (var activity in upcomingFollowUps)
        {
            var school = activity.Lead != null ? activity.Lead.School : "Unknown School";
            var nextAction = activity.NextAction ?? activity.Type.ToString();

            // Check if we already sent a reminder for this activity
            var titleCheck = $"Follow-up: {school}";
            var alreadySent = await _unitOfWork.Notifications.Query()
                .AnyAsync(n => n.UserId == activity.FoId
                    && n.Title.Contains(titleCheck)
                    && n.CreatedAt.Date == DateTime.UtcNow.Date);

            if (!alreadySent)
            {

                await _unitOfWork.Notifications.AddAsync(new Notification
                {
                    UserId = activity.FoId,
                    Type = NotificationType.Reminder,
                    Title = $"Follow-up: {school}",
                    Body = $"You have a scheduled {nextAction} tomorrow ({tomorrow:MMM dd}) at {school}.",
                    IsRead = false
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CreateLeadOverdueRemindersAsync()
    {
        var fiveDaysAgo = DateTime.UtcNow.AddDays(-5);
        var activeStages = new[] { LeadStage.NewLead, LeadStage.Contacted, LeadStage.Qualified, LeadStage.DemoStage, LeadStage.DemoDone, LeadStage.ProposalSent, LeadStage.Negotiation, LeadStage.ContractSent };

        var overdueLeads = await _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Where(l => activeStages.Contains(l.Stage)
                && (l.LastActivityDate == null || l.LastActivityDate < fiveDaysAgo)
                && l.UpdatedAt < fiveDaysAgo)
            .ToListAsync();

        foreach (var lead in overdueLeads)
        {
            var title = $"Lead overdue: {lead.School}";
            var alreadySent = await _unitOfWork.Notifications.Query()
                .AnyAsync(n => n.UserId == lead.FoId && n.Title == title && n.CreatedAt.Date == DateTime.UtcNow.Date);
            if (alreadySent) continue;

            await CreateNotificationAsync(lead.FoId, NotificationType.Warning, title, $"No activity on {lead.School} for 5+ days. Please follow up.");

            if (lead.Fo?.ZoneId != null)
            {
                var zh = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == lead.Fo.ZoneId);
                if (zh != null)
                    await CreateNotificationAsync(zh.Id, NotificationType.Warning, title, $"{lead.Fo.Name} has no activity on {lead.School} for 5+ days.");
            }
        }
    }

    public async Task CreateDemoTomorrowRemindersAsync()
    {
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        var dayAfter = tomorrow.AddDays(1);

        var demos = await _unitOfWork.DemoAssignments.Query()
            .Include(d => d.School)
            .Where(d => d.ScheduledDate >= tomorrow && d.ScheduledDate < dayAfter && d.Status != DemoStatus.Completed && d.Status != DemoStatus.Cancelled)
            .ToListAsync();

        foreach (var demo in demos)
        {
            var title = $"Demo tomorrow: {demo.School?.Name ?? "School"}";
            var alreadySent = await _unitOfWork.Notifications.Query()
                .AnyAsync(n => n.UserId == demo.AssignedToId && n.Title == title && n.CreatedAt.Date == DateTime.UtcNow.Date);
            if (!alreadySent)
                await CreateNotificationAsync(demo.AssignedToId, NotificationType.Reminder, title, $"You have a demo scheduled tomorrow at {demo.School?.Name ?? "School"}.");
        }
    }

    public async Task CreateLateStartRemindersAsync()
    {
        // Check IST 10 AM — only run this check between 4:30-5:30 UTC (10:00-11:00 IST)
        var utcNow = DateTime.UtcNow;
        if (utcNow.Hour < 4 || utcNow.Hour > 5) return;

        var todayIst = DateTime.UtcNow.Date; // approximate
        var fos = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.FO && u.IsActive)
            .ToListAsync();

        foreach (var fo in fos)
        {
            var hasSession = await _unitOfWork.TrackingSessions.Query()
                .AnyAsync(s => s.UserId == fo.Id && s.SessionDate.Date == todayIst && s.Status == TrackingSessionStatus.Active);
            if (hasSession) continue;

            var title = $"{fo.Name} not started";
            var alreadySent = await _unitOfWork.Notifications.Query()
                .AnyAsync(n => n.Title == title && n.CreatedAt.Date == DateTime.UtcNow.Date);
            if (alreadySent) continue;

            if (fo.ZoneId != null)
            {
                var zh = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == fo.ZoneId);
                if (zh != null)
                    await CreateNotificationAsync(zh.Id, NotificationType.Warning, title, $"{fo.Name} hasn't started tracking by 10 AM IST.");
            }
        }
    }
}
