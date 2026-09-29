namespace SignalR_Demo.Models.Chat_Models;

public class ChatParticipant
{
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime LastReadAt { get; set; }
}
