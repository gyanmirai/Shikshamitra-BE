using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Portal;

public class StudentAppAccessEvent : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid RecipientUserId { get; private set; }
	public string Relationship { get; private set; } = string.Empty;
	public string? DisplayName { get; private set; }
	public bool Enabled { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public DateTime ChangedAt { get; private set; }
	public Guid ChangedByUserId { get; private set; }
	public string? ChangedByName { get; private set; }

	protected StudentAppAccessEvent() { }

	public StudentAppAccessEvent(Guid id, Guid tenantId, Guid studentId, Guid recipientUserId, string relationship,
		string? displayName, bool enabled, string reason, DateTime changedAt, Guid changedByUserId, string? changedByName) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		RecipientUserId = recipientUserId;
		Relationship = relationship;
		DisplayName = displayName;
		Enabled = enabled;
		Reason = reason;
		ChangedAt = changedAt;
		ChangedByUserId = changedByUserId;
		ChangedByName = changedByName;
	}
}
