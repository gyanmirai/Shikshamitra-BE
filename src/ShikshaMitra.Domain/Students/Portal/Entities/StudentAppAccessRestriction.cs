using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Portal;

/// <summary>A manual restriction for one child. A null user denotes the student's own account.</summary>
public class StudentAppAccessRestriction : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid? UserId { get; private set; }
	public bool IsOff { get; private set; }

	protected StudentAppAccessRestriction() { }

	public StudentAppAccessRestriction(Guid id, Guid tenantId, Guid studentId, Guid? userId, bool isOff) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		UserId = userId;
		IsOff = isOff;
	}

	public void SetEnabled(bool enabled) => IsOff = !enabled;
}
