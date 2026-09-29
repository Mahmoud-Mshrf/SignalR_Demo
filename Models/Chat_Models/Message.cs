namespace SignalR_Demo.Models.Chat_Models;

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