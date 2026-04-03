namespace SalesCRM.Core.Interfaces;

public interface IPushNotificationService
{
    Task SendPushAsync(int userId, string title, string body, string type = "Info");
    Task SendPushToRoleAsync(string role, string title, string body, string type = "Info", int? zoneId = null, int? regionId = null);
}
