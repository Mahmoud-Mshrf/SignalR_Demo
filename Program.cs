var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(c=>c.AddPolicy("Frontend",p=>p
    .WithOrigins("http://localhost:5000")// frontend address 
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // SignalR requires this


var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();


app.MapHub<NotficationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<DashboardHub>("/hub/dashboard");

app.Run();
