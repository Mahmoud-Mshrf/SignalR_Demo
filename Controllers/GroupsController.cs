using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SignalR_Demo.Dtos;
using SignalR_Demo.Hubs;
using SignalR_Demo.Services;

namespace SignalR_Demo.Controllers;

[Authorize]
[ApiController]
[Route("api/groups")]
public sealed class GroupsController(
    IGroupService groupService,
    IHubContext<ChatHub> chatHub,
    IHubConnectionTracker connectionTracker) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<GroupSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var groups = await groupService.GetJoinedGroupsAsync(userId, cancellationToken);
        return Ok(groups);
    }

    [HttpPost]
    [ProducesResponseType<GroupCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateGroup(
        CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var creatorId))
            return Unauthorized();

        var groupId = await groupService.CreateGroupAsync(
            request.GroupName,
            creatorId,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetMembers),
            new { groupId },
            new GroupCreatedResponse(groupId));
    }

    [HttpGet("{groupId:guid}/members")]
    [ProducesResponseType<List<GroupUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMembers(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var members = await groupService.GetGroupMembersAsync(
            userId,
            groupId,
            cancellationToken);

        return Ok(members);
    }

    [HttpGet("{groupId:guid}/messages")]
    [ProducesResponseType<List<SendMessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var messages = await groupService.GetGroupMessagesAsync(
            groupId,
            userId,
            cancellationToken);

        return Ok(messages);
    }

    [HttpGet("{groupId:guid}/users")]
    [ProducesResponseType<List<GroupUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsersWithMembership(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var managerId))
            return Unauthorized();

        var users = await groupService.GetAllUsersWithMembershipAsync(
            managerId,
            groupId,
            cancellationToken);

        return Ok(users);
    }

    [HttpPost("{groupId:guid}/members")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(
        Guid groupId,
        AddGroupParticipantRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var managerId))
            return Unauthorized();

        var added = await groupService.AddParticipantAsync(
            managerId,
            request.NewParticipantId,
            groupId,
            cancellationToken);

        if (!added)
            return Conflict(new ProblemDetails
            {
                Title = "The user is already a group member.",
                Status = StatusCodes.Status409Conflict
            });

        return CreatedAtAction(nameof(GetMembers), new { groupId }, null);
    }

    [HttpDelete("{groupId:guid}/members/{participantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(
        Guid groupId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var managerId))
            return Unauthorized();

        var removed = await groupService.RemoveParticipantAsync(
            managerId,
            participantId,
            groupId,
            cancellationToken);

        if (!removed)
            return NotFound();

        foreach (var connectionId in connectionTracker.GetConnectionIds(participantId))
        {
            await chatHub.Groups.RemoveFromGroupAsync(
                connectionId,
                ChatHub.RoomGroup(groupId),
                cancellationToken);
        }

        return NoContent();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}