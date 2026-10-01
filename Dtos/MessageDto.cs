using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Dtos;

public sealed record MessageDto(Guid ChatId , Guid SenderId ,string Content, DateTime SentAt);
public sealed record ChatMessageDto(Guid SenderId ,string Content, DateTime SentAt);
public sealed record ChatDto(Guid ChatId , Guid ReceiverId,string ReceiverName,string Content , DateTime SentAt,bool SentByMe,int UnreadMessages);
