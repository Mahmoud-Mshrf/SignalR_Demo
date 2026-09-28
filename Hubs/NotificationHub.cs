using Microsoft.AspNetCore.SignalR;

namespace SignalR_Demo.Hubs;

public class NotificationHub : Hub
{
    public async Task SendAll(string text)
    {
        await Clients.All.SendAsync("ReceiveText",text);
    }
}

public class ChatHub : Hub
{
    public async Task SendAll(string text)
    {
        await Clients.All.SendAsync("ReceiveText",text);
    }
}

public class DashboardHub : Hub
{
    public async Task SendAll(string text)
    {
        await Clients.All.SendAsync("ReceiveText",text);
    }
}