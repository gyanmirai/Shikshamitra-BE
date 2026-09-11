using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryManagement : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid InquiryId { get; private set; }
	public Guid StageDefinitionId { get; private set; }
	public string PolicySnapshotJson { get; private set; } = string.Empty;
	public DateTime? FirstContactDueAt { get; private set; }
	public DateTime? QualificationDueAt { get; private set; }
	public DateTime? LastContactAt { get; private set; }
	public int ContactCount { get; private set; }
	public AdmissionInquiryQualificationOutcome? Outcome { get; private set; }
	public string? QualificationAnswersJson { get; private set; }
	public string? OutcomeReason { get; private set; }
	public DateTime? OutcomeAt { get; private set; }
	public Guid? OutcomeBy { get; private set; }
	public int Revision { get; private set; }

	private AdmissionInquiryManagement() { }

	public AdmissionInquiryManagement(Guid id, Guid? tenantId, Guid inquiryId, Guid stageDefinitionId,
		string policySnapshotJson, DateTime createdAt, int? firstContactDueInHours, int? qualificationDueInDays) : base(id)
	{
		TenantId = tenantId;
		InquiryId = Check.NotDefaultOrNull<Guid>(inquiryId, nameof(inquiryId));
		StageDefinitionId = Check.NotDefaultOrNull<Guid>(stageDefinitionId, nameof(stageDefinitionId));
		PolicySnapshotJson = Check.NotNullOrWhiteSpace(policySnapshotJson, nameof(policySnapshotJson));
		FirstContactDueAt = firstContactDueInHours.HasValue ? createdAt.ToUniversalTime().AddHours(firstContactDueInHours.Value) : null;
		QualificationDueAt = qualificationDueInDays.HasValue ? createdAt.ToUniversalTime().AddDays(qualificationDueInDays.Value) : null;
	}

	public void RecordContact(DateTime contactedAt)
	{
		LastContactAt = contactedAt.ToUniversalTime();
		ContactCount++;
		Revision++;
	}

	public void Touch() => Revision++;

	public void RecordOutcome(AdmissionInquiryQualificationOutcome outcome, string answersJson, string reason,
		Guid actor, DateTime decidedAt)
	{
		if (!Enum.IsDefined(outcome) || actor == Guid.Empty || string.IsNullOrWhiteSpace(answersJson) ||
			string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1024)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		Outcome = outcome;
		QualificationAnswersJson = answersJson;
		OutcomeReason = reason.Trim();
		OutcomeBy = actor;
		OutcomeAt = decidedAt.ToUniversalTime();
		Revision++;
	}

	public void Reopen()
	{
		if (!Outcome.HasValue) throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
		Outcome = null;
		QualificationAnswersJson = null;
		OutcomeReason = null;
		OutcomeAt = null;
		OutcomeBy = null;
		Revision++;
	}
}

public class AdmissionInquiryFollowUp : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid InquiryId { get; private set; }
	public Guid ManagementId { get; private set; }
	public Guid? AssignedCounsellorId { get; private set; }
	public DateTime ScheduledAt { get; private set; }
	public string Purpose { get; private set; } = string.Empty;
	public AdmissionInquiryFollowUpStatus Status { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public Guid? CompletedBy { get; private set; }
	public string? CompletionNote { get; private set; }

	private AdmissionInquiryFollowUp() { }

	public AdmissionInquiryFollowUp(Guid id, Guid? tenantId, Guid inquiryId, Guid managementId,
		Guid? assignedCounsellorId, DateTime scheduledAt, string purpose) : base(id)
	{
		if (assignedCounsellorId == Guid.Empty || scheduledAt == default || string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length > 512)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		TenantId = tenantId;
		InquiryId = Check.NotDefaultOrNull<Guid>(inquiryId, nameof(inquiryId));
		ManagementId = Check.NotDefaultOrNull<Guid>(managementId, nameof(managementId));
		AssignedCounsellorId = assignedCounsellorId;
		ScheduledAt = scheduledAt.ToUniversalTime();
		Purpose = purpose.Trim();
		Status = AdmissionInquiryFollowUpStatus.Scheduled;
	}

	public void Complete(Guid actor, DateTime completedAt, string note)
	{
		if (Status != AdmissionInquiryFollowUpStatus.Scheduled)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
		if (actor == Guid.Empty || string.IsNullOrWhiteSpace(note) || note.Trim().Length > 1024)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		Status = AdmissionInquiryFollowUpStatus.Completed;
		CompletedBy = actor;
		CompletedAt = completedAt.ToUniversalTime();
		CompletionNote = note.Trim();
	}

	public void Cancel(Guid actor, DateTime cancelledAt, string reason)
	{
		if (Status != AdmissionInquiryFollowUpStatus.Scheduled)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
		if (actor == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1024)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		Status = AdmissionInquiryFollowUpStatus.Cancelled;
		CompletedBy = actor;
		CompletedAt = cancelledAt.ToUniversalTime();
		CompletionNote = reason.Trim();
	}
}
