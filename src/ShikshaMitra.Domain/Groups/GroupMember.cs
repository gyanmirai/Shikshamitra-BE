using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Groups;

/// <summary>
/// Represents a manually selected member for name-based groups
/// </summary>
public class GroupMember : Entity<Guid>
{
    /// <summary>
    /// Parent group ID
    /// </summary>
    public Guid GroupId { get; private set; }

    /// <summary>
    /// Type of member (Student or Employee)
    /// </summary>
    public GroupMemberType MemberType { get; private set; }

    /// <summary>
    /// ID of the student or employee entity
    /// </summary>
    public Guid MemberId { get; private set; }

    /// <summary>
    /// User ID associated with the member (from Student.UserId or Employee.UserId)
    /// Cached for quick membership lookups
    /// </summary>
    public Guid? UserId { get; private set; }

    protected GroupMember() { }

    public GroupMember(
        Guid id,
        Guid groupId,
        GroupMemberType memberType,
        Guid memberId,
        Guid? userId = null)
        : base(id)
    {
        GroupId = groupId;
        MemberType = memberType;
        MemberId = memberId;
        UserId = userId;
    }

    public void SetUserId(Guid? userId)
    {
        UserId = userId;
    }
}
