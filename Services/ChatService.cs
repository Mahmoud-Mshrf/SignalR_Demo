using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data;
using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Services;

public class ChatService(AppDbContext context,Guid currentUserId) : IChatService
{
    public async Task<Guid> CreateChat(Guid OtherUserId)
    {
        var currentUser = await context.Users.FindAsync(currentUserId) ?? 
            throw new ArgumentNullException("OtherUser is not found");

        var otherUser = await context.Users.FindAsync(OtherUserId) ?? 
            throw new ArgumentNullException("OtherUser is not found");
        
        var firstId = currentUserId.CompareTo(OtherUserId) < 0 ?
            currentUserId : OtherUserId;

        var secondId = currentUserId.CompareTo(OtherUserId) < 0 ?
            OtherUserId : currentUserId;
        
        var pairKey= $"{firstId}_{secondId}";

        var existingChat = await context.Chats.FirstOrDefaultAsync(x=>x.UserPairKey == pairKey);

        if (existingChat is not null )
        {
            return existingChat.Id;
        }

        var chat = Chat.Create(currentUserId,OtherUserId);

        chat.Participants.Add(new ChatParticipant
        {
            ChatId = chat.Id,
            UserId=currentUserId,
            LastReadAt= DateTime.UtcNow
        });
        
        chat.Participants.Add(new ChatParticipant
        {
            ChatId = chat.Id,
            UserId=OtherUserId,
            LastReadAt= DateTime.UtcNow
        });

        context.Chats.Add(chat);

        await context.SaveChangesAsync();

        return chat.Id;
    }
}

public interface IChatService
{
    Task<Guid> CreateChat(Guid OtherUserId);
}