using Microsoft.AspNetCore.SignalR;

namespace SignalR_Demo.Hubs;


public interface INotificationClient
{
    Task ReceiveNotification(NotificationDto notificationDto);
}

public class NotificationHub : Hub<INotificationClient>
{
    
}

public class NotificationDto
{
    public string NotificationTitle {get;set;}
    public object Data {get;set;}
}

