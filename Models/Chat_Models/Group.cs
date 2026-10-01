namespace SignalR_Demo.Models.Chat_Models;

public class Group
{
    public Guid Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public List<GroupMessage> Messages { get; set; } = [];
    public List<GroupParticipant> Participants { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class GroupMessage
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}


public class GroupParticipant
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime LastReadAt { get; set; }
}
