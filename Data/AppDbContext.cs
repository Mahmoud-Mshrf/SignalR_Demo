using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data.Configurations;
using SignalR_Demo.Models;
using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ChatConfiguration());
        modelBuilder.ApplyConfiguration(new ChatParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
    }
}