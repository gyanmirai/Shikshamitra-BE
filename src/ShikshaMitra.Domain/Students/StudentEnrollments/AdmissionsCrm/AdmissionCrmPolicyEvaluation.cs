using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyEvaluation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid PolicyAdoptionId { get; private set; }
	public string RuleCode { get; private set; } = string.Empty;
	public AdmissionCrmPolicyRuleType RuleType { get; private set; }
	public AdmissionCrmPolicyRuleSeverity Severity { get; private set; }
	public AdmissionCrmPolicyEvaluationStatus Status { get; private set; }
	public string InputSnapshotJson { get; private set; } = "{}";
	public string EvidenceJson { get; private set; } = "[]";
	public string Explanation { get; private set; } = string.Empty;
	public Guid EvaluatedByUserId { get; private set; }
	public DateTime EvaluatedAt { get; private set; }
	public Guid? ExceptionId { get; private set; }

	protected AdmissionCrmPolicyEvaluation() { }

	public AdmissionCrmPolicyEvaluation(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		Guid policyAdoptionId, string ruleCode, AdmissionCrmPolicyRuleType ruleType,
		AdmissionCrmPolicyRuleSeverity severity, AdmissionCrmPolicyEvaluationStatus status,
		string inputSnapshotJson, string evidenceJson, string explanation, Guid evaluatedByUserId,
		DateTime evaluatedAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		PolicyAdoptionId = policyAdoptionId;
		RuleCode = Check.NotNullOrWhiteSpace(ruleCode, nameof(ruleCode), AdmissionCrmConsts.MaxPolicyCodeLength).ToUpperInvariant();
		RuleType = ruleType;
		Severity = severity;
		Status = status;
		InputSnapshotJson = Check.NotNullOrWhiteSpace(inputSnapshotJson, nameof(inputSnapshotJson), AdmissionCrmConsts.MaxPolicyEvaluationJsonLength);
		EvidenceJson = Check.NotNullOrWhiteSpace(evidenceJson, nameof(evidenceJson), AdmissionCrmConsts.MaxPolicyEvaluationJsonLength);
		Explanation = Check.NotNullOrWhiteSpace(explanation, nameof(explanation), AdmissionCrmConsts.MaxNotesLength);
		EvaluatedByUserId = evaluatedByUserId;
		EvaluatedAt = evaluatedAt.ToUniversalTime();
	}

	public void ApplyApprovedException(Guid exceptionId)
	{
		ExceptionId = exceptionId;
		Status = AdmissionCrmPolicyEvaluationStatus.ExceptionApproved;
	}
}
