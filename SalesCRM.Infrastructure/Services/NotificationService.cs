using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
}
