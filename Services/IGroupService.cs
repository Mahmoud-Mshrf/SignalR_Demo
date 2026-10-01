using SignalR_Demo.Dtos;

namespace SignalR_Demo.Services;

public interface IGroupService
{
    Task<Guid> CreateGroupAsync(
        string groupName,
        Guid creatorId,
        CancellationToken cancellationToken = default);

    Task<SendMessageDto> AddGroupMessageAsync(
        Guid groupId,
        string content,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<SendMessageDto>> GetGroupMessagesAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<GroupSummaryDto>> GetJoinedGroupsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task MarkGroupAsReadAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken = default);

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

    Task<List<Guid>> GetJoinedGroupIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsGroupMemberAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<List<GroupUserDto>> GetAllUsersWithMembershipAsync(
        Guid managerId,
        Guid groupId,
        CancellationToken cancellationToken = default);
}