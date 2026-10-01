using System.ComponentModel.DataAnnotations;

namespace SignalR_Demo.Dtos;

/// <summary>Request payload for creating a group.</summary>
public sealed record CreateGroupRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public required string GroupName { get; init; }
}

/// <summary>Request payload for adding a user to a group.</summary>
public sealed record AddGroupParticipantRequest
{
    public required Guid NewParticipantId { get; init; }
}

/// <summary>Identifier of a newly created group.</summary>
public sealed record GroupCreatedResponse(Guid GroupId);