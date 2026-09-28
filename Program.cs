using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SignalR_Demo.Hubs;
using SignalR_Demo.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(c=>c.AddPolicy("Frontend",p=>p
    .WithOrigins("http://localhost:5000")// frontend address 
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // SignalR requires this

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SigningKey"]))
        };
    });
        builder.Services.AddScoped<ITokenService, JwtTokenService>();
        builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();


app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<DashboardHub>("/hub/dashboard");

app.Run();
