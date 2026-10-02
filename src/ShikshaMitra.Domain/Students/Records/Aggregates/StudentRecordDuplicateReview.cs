using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

/// <summary>A staff review preserves both permanent identities; merging is not an operation here.</summary>
public class StudentRecordDuplicateReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid OtherStudentId { get; private set; }
	public Guid RequestedBy { get; private set; }
	public Guid? ReviewedBy { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string? ReviewReason { get; private set; }
	public string Status { get; private set; } = "Open";
	protected StudentRecordDuplicateReview() { }
	public StudentRecordDuplicateReview(Guid id, Guid tenant, Guid student, Guid other, Guid actor, string reason) : base(id)
	{
		if (student == other || student == Guid.Empty || other == Guid.Empty) throw new BusinessException("Students:Records:InvalidDuplicatePair");
		TenantId = tenant; StudentId = student.CompareTo(other) < 0 ? student : other;
		OtherStudentId = student.CompareTo(other) < 0 ? other : student;
		RequestedBy = actor; Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000, 5).Trim();
	}
	public void Resolve(Guid actor, bool confirmed, string reason)
	{
		if (Status != "Open") throw new BusinessException("Students:Records:DuplicateReviewClosed");
		if (actor == RequestedBy) throw new BusinessException("Students:Records:SelfApproval");
		ReviewedBy = actor; ReviewReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000, 5).Trim();
		Status = confirmed ? "ConfirmedDuplicate" : "NotDuplicate";
	}
}
