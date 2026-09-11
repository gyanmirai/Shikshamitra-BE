using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionEligibilityEvaluation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public int SequenceNumber { get; private set; }
	public string Outcome { get; private set; } = string.Empty;
	public string PolicySnapshotJson { get; private set; } = string.Empty;
	public string FactsSnapshotJson { get; private set; } = string.Empty;
	public string CriteriaSnapshotJson { get; private set; } = string.Empty;
	public string InputFingerprint { get; private set; } = string.Empty;
	public Guid EvaluatedBy { get; private set; }
	public DateTime EvaluatedAt { get; private set; }
	protected AdmissionEligibilityEvaluation() { }
	public AdmissionEligibilityEvaluation(Guid id, Guid? tenantId, Guid applicationId, Guid stageInstanceId, int sequenceNumber,
		string outcome, string policyJson, string factsJson, string criteriaJson, string fingerprint, Guid actor, DateTime at) : base(id)
	{
		if (outcome is not ("eligible" or "ineligible" or "needsInformation")) throw new BusinessException(AdmissionEligibilityErrorCodes.InvalidState);
		TenantId = tenantId; ApplicationId = Check.NotDefaultOrNull<Guid>(applicationId, nameof(applicationId));
		StageInstanceId = Check.NotDefaultOrNull<Guid>(stageInstanceId, nameof(stageInstanceId));
		SequenceNumber = Check.Range(sequenceNumber, nameof(sequenceNumber), 1, int.MaxValue); Outcome = outcome;
		PolicySnapshotJson = Check.NotNullOrWhiteSpace(policyJson, nameof(policyJson)); FactsSnapshotJson = Check.NotNullOrWhiteSpace(factsJson, nameof(factsJson));
		CriteriaSnapshotJson = Check.NotNullOrWhiteSpace(criteriaJson, nameof(criteriaJson));
		InputFingerprint = Check.Length(Check.NotNullOrWhiteSpace(fingerprint, nameof(fingerprint)), nameof(fingerprint), 64, 64)!;
		EvaluatedBy = Check.NotDefaultOrNull<Guid>(actor, nameof(actor)); EvaluatedAt = at;
	}
}
