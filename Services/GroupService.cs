using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data;
using SignalR_Demo.Dtos;
using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Services;

public sealed class GroupService(AppDbContext context) : IGroupService
{
    public async Task<Guid> CreateGroupAsync(
        string groupName,
        Guid creatorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            throw new ArgumentException("Group name is required.", nameof(groupName));

        groupName = groupName.Trim();
        if (groupName.Length > 100)
            throw new ArgumentException("Group name cannot exceed 100 characters.", nameof(groupName));

        if (!await context.Users.AnyAsync(user => user.Id == creatorId, cancellationToken))
            throw new KeyNotFoundException("Creator was not found.");

        var now = DateTime.UtcNow;
        var group = new Group
        {
            Id = Guid.NewGuid(),
            GroupName = groupName,
            CreatedAt = now,
            Participants =
            [
                new GroupParticipant
                {
                    UserId = creatorId,
                    IsAdmin = true,
                    LastReadAt = now
                }
            ]
        };

        context.Groups.Add(group);
        await context.SaveChangesAsync(cancellationToken);

        return group.Id;
    }

    public async Task<SendMessageDto> AddGroupMessageAsync(Guid groupId, string content, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content is required.", nameof(content));

        content = content.Trim();
        if (content.Length > 4000)
            throw new ArgumentException("Message content cannot exceed 4000 characters.", nameof(content));

        await GetRequiredGroupParticipantAsync(
            userId,
            groupId,
            "Only group members can send messages.");

        var message = new GroupMessage
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SenderId = userId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        context.GroupMessages.Add(message);
        await context.SaveChangesAsync();

        return new SendMessageDto(groupId,message.SenderId,message.Content,message.SentAt);
    }

    public async Task<List<SendMessageDto>> GetGroupMessagesAsync(Guid groupId, Guid userId)
    {
        await GetRequiredGroupParticipantAsync(
            userId,
            groupId,
            "Only group members can view messages.");

        return await context.GroupMessages
            .Where(message => message.GroupId == groupId)
            .OrderBy(message => message.SentAt)
            .Select(message => new SendMessageDto(
                message.GroupId,
                message.SenderId,
                message.Content,
                message.SentAt))
            .ToListAsync();
    }

    public async Task<bool> AddParticipantAsync(
        Guid managerId,
        Guid newParticipantId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(managerId, groupId, cancellationToken);

        if (!await context.Users.AnyAsync(user => user.Id == newParticipantId, cancellationToken))
            throw new KeyNotFoundException("User was not found.");

        var alreadyParticipant = await context.GroupParticipants.AnyAsync(participant =>
            participant.GroupId == groupId && participant.UserId == newParticipantId,
            cancellationToken);

        if (alreadyParticipant)
            return false;

        context.GroupParticipants.Add(new GroupParticipant
        {
            GroupId = groupId,
            UserId = newParticipantId,
            IsAdmin = false,
            LastReadAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<GroupUserDto>> GetGroupMembersAsync(
        Guid currentUserId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await GetRequiredGroupParticipantAsync(
            currentUserId,
            groupId,
            "Only group members can view the member list.",
            cancellationToken);

        return await context.GroupParticipants
            .Where(participant => participant.GroupId == groupId)
            .OrderBy(participant => participant.User.Name)
            .Select(participant => new GroupUserDto(
                participant.UserId,
                participant.User.Name,
                true))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Guid>> GetJoinedGroupIdsAsync(Guid userId)
    {
        return context.GroupParticipants
            .Where(participant => participant.UserId == userId)
            .Select(participant => participant.GroupId)
            .ToListAsync();
    }

    public Task<bool> IsGroupMemberAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        return context.GroupParticipants.AnyAsync(
            participant => participant.UserId == userId && participant.GroupId == groupId,
            cancellationToken);
    }

    public async Task<List<GroupUserDto>> GetAllUsersWithMembershipAsync(
        Guid managerId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(managerId, groupId, cancellationToken);

        return await context.Users
            .OrderBy(user => user.Name)
            .Select(user => new GroupUserDto(
                user.Id,
                user.Name,
                context.GroupParticipants.Any(participant =>
                    participant.GroupId == groupId && participant.UserId == user.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RemoveParticipantAsync(
        Guid managerId,
        Guid participantId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(managerId, groupId, cancellationToken);

        var participant = await context.GroupParticipants.FirstOrDefaultAsync(item =>
            item.GroupId == groupId && item.UserId == participantId,
            cancellationToken);

        if (participant is null)
            return false;

        if (participant.IsAdmin)
            throw new InvalidOperationException("The group admin cannot be removed.");

        context.GroupParticipants.Remove(participant);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureManagerAsync(
        Guid managerId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var manager = await GetRequiredGroupParticipantAsync(
            managerId,
            groupId,
            "Only group members can manage members.",
            cancellationToken);

        if (!manager.IsAdmin)
            throw new UnauthorizedAccessException("Only the group admin can manage members.");
    }

    private async Task<GroupParticipant> GetRequiredGroupParticipantAsync(
        Guid userId,
        Guid groupId,
        string unauthorizedMessage,
        CancellationToken cancellationToken = default)
    {
        var participant = await context.GroupParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.GroupId == groupId && item.UserId == userId,
                cancellationToken);

        if (participant is not null)
            return participant;

        if (!await context.Groups.AnyAsync(group => group.Id == groupId, cancellationToken))
            throw new KeyNotFoundException("Group was not found.");

        throw new UnauthorizedAccessException(unauthorizedMessage);
    }
}

public interface IGroupService
{
    Task<Guid> CreateGroupAsync(
        string groupName,
        Guid creatorId,
        CancellationToken cancellationToken = default);
    Task<SendMessageDto> AddGroupMessageAsync(Guid groupId, string content, Guid userId);
    Task<List<SendMessageDto>> GetGroupMessagesAsync(Guid groupId, Guid userId);
    Task<bool> AddParticipantAsync(
        Guid managerId,
        Guid newParticipantId,
        Guid groupId,
        CancellationToken cancellationToken = default);
    Task<bool> RemoveParticipantAsync(
        Guid managerId,
        Guid participantId,
        Guid groupId,
        CancellationToken cancellationToken = default);
    Task<List<GroupUserDto>> GetGroupMembersAsync(
        Guid currentUserId,
        Guid groupId,
        CancellationToken cancellationToken = default);
    Task<List<Guid>> GetJoinedGroupIdsAsync(Guid userId);
    Task<bool> IsGroupMemberAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken = default);
    Task<List<GroupUserDto>> GetAllUsersWithMembershipAsync(
        Guid managerId,
        Guid groupId,
        CancellationToken cancellationToken = default);
}