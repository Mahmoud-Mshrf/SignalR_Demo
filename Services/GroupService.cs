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
                    LastReadAt = now
                }
            ]
        };

        context.Groups.Add(group);
        await context.SaveChangesAsync();

        return group.Id;
    }

    public async Task<bool> AddParticipantAsync(Guid newParticipantId, Guid groupId)
    {
        if (!await context.Groups.AnyAsync(group => group.Id == groupId))
            throw new KeyNotFoundException("Group was not found.");

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
            LastReadAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return true;
    }

    public Task<List<GroupUserDto>> GetUsersExceptAsync(Guid currentUserId)
    {
        return context.Users
            .Where(user => user.Id != currentUserId)
            .OrderBy(user => user.Name)
            .Select(user => new GroupUserDto(user.Id, user.Name))
            .ToListAsync();
    }
}

public interface IGroupService
{
    Task<Guid> CreateGroupAsync(string groupName, Guid creatorId);
    Task<bool> AddParticipantAsync(Guid newParticipantId, Guid groupId);
    Task<List<GroupUserDto>> GetUsersExceptAsync(Guid currentUserId);
}