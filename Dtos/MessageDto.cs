using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Dtos;

public sealed record MessageDto(Guid ChatId , Guid SenderId ,string Content, DateTime SentAt);
