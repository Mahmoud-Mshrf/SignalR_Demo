using Microsoft.AspNetCore.SignalR;

namespace SignalR_Demo.Hubs;

public interface INotificationClient
{
    Task ReceiveNotification(NotificationDto notificationDto);
}

public sealed class NotificationHub : Hub<INotificationClient>;

public sealed class NotificationDto
{
    public string NotificationTitle { get; set; } = string.Empty;
    public object Data { get; set; } = string.Empty;
}

