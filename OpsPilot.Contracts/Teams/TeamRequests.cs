namespace OpsPilot.Contracts.Teams;

/// <summary>Creates a team. The owning tenant comes from the bearer token.</summary>
public sealed record CreateTeamRequest(string Name, string? Description = null);

/// <summary>
/// Adds a user to a team. <paramref name="Role"/> defaults to <c>Member</c> when omitted; the
/// only other value is <c>Lead</c>.
/// </summary>
public sealed record AddTeamMemberRequest(Guid UserId, string? Role = null);
