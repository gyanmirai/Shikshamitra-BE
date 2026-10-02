using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentGuardianAccessReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid ChangeRequestId { get; private set; }
	public string PreviousLinksJson { get; private set; } = "[]";
	public string Status { get; private set; } = "Pending";
	public string OnApprovedChangeMode { get; private set; } = "SuspendUntilReview";
	public string? ResolutionJson { get; private set; }
	public Guid? ReviewedByUserId { get; private set; }
	public DateTime? ReviewedAt { get; private set; }
	protected StudentGuardianAccessReview() { }
	public StudentGuardianAccessReview(Guid id, Guid tenant, Guid student, Guid change, string snapshot, string mode) : base(id)
	{
		TenantId = tenant; StudentId = student; ChangeRequestId = change; PreviousLinksJson = snapshot; OnApprovedChangeMode = mode;
	}
	public void Resolve(Guid actor, DateTime at, string resolution)
	{
		if (Status != "Pending") throw new BusinessException("Students:Records:GuardianReviewAlreadyResolved");
		if (actor == Guid.Empty) throw new BusinessException("Students:Records:UserRequired");
		Status = "Completed"; ReviewedByUserId = actor; ReviewedAt = at; ResolutionJson = resolution;
	}
}
