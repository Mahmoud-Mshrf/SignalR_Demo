using System.ComponentModel.DataAnnotations.Schema;

namespace SignalR_Demo.Models.Chat_Models;

public class Chat
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string UserPairKey {get; private set;} = null!;
    public List<ChatParticipant> Participants { get; set; } = [];
    public List<Message> Messages { get; set; } = [];


    public static Chat Create(Guid userAId, Guid userBId)
    {
        if (userAId == userBId)
            throw new ArgumentException("A user cannot chat with themselves.");

        var firstId = userAId.CompareTo(userBId) < 0
            ? userAId
            : userBId;

        var secondId = userAId.CompareTo(userBId) < 0
            ? userBId
            : userAId;

        return new Chat
        {
            Id = Guid.NewGuid(),
            UserPairKey = $"{firstId}_{secondId}",
            CreatedAt = DateTime.UtcNow
        };
    }
}
