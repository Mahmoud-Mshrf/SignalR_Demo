using System.ComponentModel.DataAnnotations.Schema;

namespace SignalR_Demo.Models.Chat_Models;

public class Chat
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ChatParticipant> Participants { get; set; } = [];
    public List<Message> Messages { get; set; } = [];
}

public class ChatParticipant
{
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime LastReadAt { get; set; }
}

public class Message
{
    public Guid Id { get; set; }

    public Guid ChatId { get; set; }
    public Chat Chat { get; set; } = null!;

    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}