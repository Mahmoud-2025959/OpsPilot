using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

public sealed class Team : TenantOwnedEntity
{
    private readonly List<TeamMember> _members = [];

    private Team(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public IReadOnlyCollection<TeamMember> Members => _members;

    public static Team Create(Guid tenantId, string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Team(name.Trim(), description?.Trim())
        {
            TenantId = tenantId
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public TeamMember AddMember(Guid userId, TeamRole role, DateTimeOffset? joinedAt = null)
    {
        if (_members.Any(m => m.UserId == userId))
        {
            throw new DomainException($"User '{userId}' is already a member of team '{Name}'.");
        }

        var member = TeamMember.Create(Id, userId, TenantId, role, joinedAt);
        _members.Add(member);
        return member;
    }

    public bool RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        return member is not null && _members.Remove(member);
    }

    public void ChangeMemberRole(Guid userId, TeamRole role)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new DomainException($"User '{userId}' is not a member of team '{Name}'.");
        member.ChangeRole(role);
    }
}
