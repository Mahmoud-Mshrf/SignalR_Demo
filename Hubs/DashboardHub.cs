using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalR_Demo.Models;

namespace SignalR_Demo.Hubs;

[Authorize(Roles = nameof(UserRole.Manager) + "," + nameof(UserRole.SuperAdmin))]
public class DashboardHub : Hub
{
    public async Task SendAll(string text)
    {
        await Clients.All.SendAsync("ReceiveText",text);
    }
}