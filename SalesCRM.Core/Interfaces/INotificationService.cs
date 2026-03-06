using SalesCRM.Core.DTOs;

namespace SalesCRM.Core.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetNotificationsAsync(int userId);
    Task MarkAsReadAsync(int notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
}
