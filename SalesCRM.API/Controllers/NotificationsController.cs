using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _uow;

    public NotificationsController(INotificationService notificationService, IUnitOfWork uow)
    {
        _notificationService = notificationService;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var notifications = await _notificationService.GetNotificationsAsync(UserId);
        return Ok(ApiResponse<List<NotificationDto>>.Ok(notifications));
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id, UserId);
        return Ok(ApiResponse<object>.Ok(null!, "Marked as read"));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(UserId);
        return Ok(ApiResponse<object>.Ok(null!, "All marked as read"));
    }

    [HttpPost("fcm-token")]
    public async Task<IActionResult> SaveFcmToken([FromBody] FcmTokenRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(UserId);
        if (user == null) return NotFound();
        user.FcmToken = request.Token;
        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null!, "FCM token saved"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        await _notificationService.MarkAsReadAsync(id, UserId);
        return Ok(ApiResponse<object>.Ok(null!, "Notification dismissed"));
    }
}
