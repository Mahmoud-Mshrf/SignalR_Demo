using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data;
using SignalR_Demo.Dtos;
using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Services;

public sealed class GroupService(AppDbContext context) : IGroupService
{
    public async Task<Guid> CreateGroupAsync(string groupName, Guid creatorId)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            throw new ArgumentException("Group name is required.", nameof(groupName));

        groupName = groupName.Trim();
        if (groupName.Length > 100)
            throw new ArgumentException("Group name cannot exceed 100 characters.", nameof(groupName));

        if (!await context.Users.AnyAsync(user => user.Id == creatorId))
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
        await context.SaveChangesAsync();

        return group.Id;
    }

    public async Task<SendMessageDto> AddGroupMessageAsync(Guid groupId, string content, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content is required.", nameof(content));

        content = content.Trim();
        if (content.Length > 4000)
            throw new ArgumentException("Message content cannot exceed 4000 characters.", nameof(content));

        if (!await context.Groups.AnyAsync(group => group.Id == groupId))
            throw new KeyNotFoundException("Group was not found.");

        var isMember = await context.GroupParticipants.AnyAsync(participant =>
            participant.GroupId == groupId && participant.UserId == userId);

        if (!isMember)
            throw new UnauthorizedAccessException("Only group members can send messages.");

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

    public async Task<bool> AddParticipantAsync(Guid managerId, Guid newParticipantId, Guid groupId)
    {
        await EnsureManagerAsync(managerId, groupId);

        if (!await context.Users.AnyAsync(user => user.Id == newParticipantId))
            throw new KeyNotFoundException("User was not found.");

        var alreadyParticipant = await context.GroupParticipants.AnyAsync(participant =>
            participant.GroupId == groupId && participant.UserId == newParticipantId);

        if (alreadyParticipant)
            return false;

        context.GroupParticipants.Add(new GroupParticipant
        {
            GroupId = groupId,
            UserId = newParticipantId,
            IsAdmin = false,
            LastReadAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<GroupUserDto>> GetGroupMembersAsync(Guid currentUserId, Guid groupId)
    {
        var isMember = await context.GroupParticipants.AnyAsync(participant =>
            participant.GroupId == groupId && participant.UserId == currentUserId);

        if (!isMember)
            throw new UnauthorizedAccessException("Only group members can view the member list.");

        return await context.GroupParticipants
            .Where(participant => participant.GroupId == groupId)
            .OrderBy(participant => participant.User.Name)
            .Select(participant => new GroupUserDto(
                participant.UserId,
                participant.User.Name,
                true))
            .ToListAsync();
    }

    public Task<List<Guid>> GetJoinedGroupIdsAsync(Guid userId)
    {
        return context.GroupParticipants
            .Where(participant => participant.UserId == userId)
            .Select(participant => participant.GroupId)
            .ToListAsync();
    }

    public async Task<List<GroupUserDto>> GetAllUsersWithMembershipAsync(Guid managerId, Guid groupId)
    {
        await EnsureManagerAsync(managerId, groupId);

        return await context.Users
            .OrderBy(user => user.Name)
            .Select(user => new GroupUserDto(
                user.Id,
                user.Name,
                context.GroupParticipants.Any(participant =>
                    participant.GroupId == groupId && participant.UserId == user.Id)))
            .ToListAsync();
    }

    public async Task<bool> RemoveParticipantAsync(Guid managerId, Guid participantId, Guid groupId)
    {
        await EnsureManagerAsync(managerId, groupId);

        var participant = await context.GroupParticipants.FirstOrDefaultAsync(item =>
            item.GroupId == groupId && item.UserId == participantId);

        if (participant is null)
            return false;

        if (participant.IsAdmin)
            throw new InvalidOperationException("The group admin cannot be removed.");

        context.GroupParticipants.Remove(participant);
        await context.SaveChangesAsync();
        return true;
    }

    private async Task EnsureManagerAsync(Guid managerId, Guid groupId)
    {
        if (!await context.Groups.AnyAsync(group => group.Id == groupId))
            throw new KeyNotFoundException("Group was not found.");

        var isManager = await context.GroupParticipants.AnyAsync(participant =>
            participant.GroupId == groupId &&
            participant.UserId == managerId &&
            participant.IsAdmin);

        if (!isManager)
            throw new UnauthorizedAccessException("Only the group admin can manage members.");
    }
}

public interface IGroupService
{
    Task<Guid> CreateGroupAsync(string groupName, Guid creatorId);
    Task<SendMessageDto> AddGroupMessageAsync(Guid groupId, string content, Guid userId);
    Task<bool> AddParticipantAsync(Guid managerId, Guid newParticipantId, Guid groupId);
    Task<bool> RemoveParticipantAsync(Guid managerId, Guid participantId, Guid groupId);
    Task<List<GroupUserDto>> GetGroupMembersAsync(Guid currentUserId, Guid groupId);
    Task<List<Guid>> GetJoinedGroupIdsAsync(Guid userId);
    Task<List<GroupUserDto>> GetAllUsersWithMembershipAsync(Guid managerId, Guid groupId);
}