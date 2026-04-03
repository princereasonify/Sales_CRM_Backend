using SalesCRM.Core.DTOs;
using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetNotificationsAsync(int userId);
    Task MarkAsReadAsync(int notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
    Task CreateNotificationAsync(int userId, NotificationType type, string title, string body);
    Task CreateFollowUpRemindersAsync();
    Task CreateLeadOverdueRemindersAsync();
    Task CreateDemoTomorrowRemindersAsync();
    Task CreateLateStartRemindersAsync();
}
