using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentifierAllocation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid ActivationId { get; private set; }
	public Guid NumberSeriesId { get; private set; }
	public Guid? StudentId { get; private set; }
	public AdmissionCrmIdentifierType IdentifierType { get; private set; }
	public long Sequence { get; private set; }
	public string Value { get; private set; } = string.Empty;
	public AdmissionCrmIdentifierStatus Status { get; private set; }
	public DateTime AllocatedAt { get; private set; }
	public DateTime? CommittedAt { get; private set; }
	public string? LifecycleReason { get; private set; }
	public bool IsManual { get; private set; }
	public Guid? AssignedByUserId { get; private set; }

	protected AdmissionCrmIdentifierAllocation() { }

	public AdmissionCrmIdentifierAllocation(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		Guid activationId, Guid numberSeriesId, AdmissionCrmIdentifierType identifierType,
		long sequence, string value, DateTime allocatedAt) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; LeadId = leadId; ActivationId = activationId;
		NumberSeriesId = numberSeriesId; IdentifierType = identifierType; Sequence = sequence;
		Value = Check.NotNullOrWhiteSpace(value, nameof(value), AdmissionCrmConsts.MaxIdentifierValueLength);
		AllocatedAt = allocatedAt.ToUniversalTime(); Status = AdmissionCrmIdentifierStatus.Allocated;
	}

	public void MarkManual(Guid userId, string reason)
	{
		if (Status != AdmissionCrmIdentifierStatus.Allocated)
			throw new BusinessException("AdmissionCrm:IdentifierNotAllocatable");
		IsManual = true; AssignedByUserId = userId;
		LifecycleReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	public void Commit(Guid studentId, DateTime at)
	{
		if (Status != AdmissionCrmIdentifierStatus.Allocated) throw new BusinessException("AdmissionCrm:IdentifierNotAllocatable");
		StudentId = studentId; CommittedAt = at.ToUniversalTime(); Status = AdmissionCrmIdentifierStatus.Committed;
	}

	public void Void(string reason)
	{
		if (Status == AdmissionCrmIdentifierStatus.Void) return;
		if (Status is not (AdmissionCrmIdentifierStatus.Allocated or AdmissionCrmIdentifierStatus.Committed))
			throw new BusinessException("AdmissionCrm:IdentifierCannotBeVoided");
		LifecycleReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmIdentifierStatus.Void;
	}
}
