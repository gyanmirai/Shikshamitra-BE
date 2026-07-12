using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDataQualityIssue : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Fingerprint { get; private set; } = string.Empty;
	public AdmissionCrmDataQualityIssueType IssueType { get; private set; }
	public AdmissionCrmDataQualityIssueSeverity Severity { get; private set; }
	public AdmissionCrmDataQualityIssueStatus Status { get; private set; }
	public Guid? LeadId { get; private set; }
	public Guid? StudentId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string DetailsJson { get; private set; } = "{}";
	public string ImpactedReportsJson { get; private set; } = "[]";
	public DateTime FirstDetectedAt { get; private set; }
	public DateTime LastDetectedAt { get; private set; }
	public Guid? OwnerUserId { get; private set; }
	public string? ResolutionReason { get; private set; }
	public Guid? ResolvedByUserId { get; private set; }
	public DateTime? ResolvedAt { get; private set; }

	protected AdmissionCrmDataQualityIssue() { }

	public AdmissionCrmDataQualityIssue(Guid id, Guid? tenantId, Guid academicYearId,
		string fingerprint, AdmissionCrmDataQualityIssueType issueType,
		AdmissionCrmDataQualityIssueSeverity severity, Guid? leadId, Guid? studentId,
		string title, string detailsJson, string impactedReportsJson, DateTime detectedAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Fingerprint = Check.NotNullOrWhiteSpace(fingerprint, nameof(fingerprint), 128);
		IssueType = issueType;
		Severity = severity;
		LeadId = leadId;
		StudentId = studentId;
		Title = Check.NotNullOrWhiteSpace(title, nameof(title), AdmissionCrmConsts.MaxNameLength);
		DetailsJson = Check.NotNullOrWhiteSpace(detailsJson, nameof(detailsJson), AdmissionCrmConsts.MaxPolicyEvaluationJsonLength);
		ImpactedReportsJson = Check.NotNullOrWhiteSpace(impactedReportsJson, nameof(impactedReportsJson), AdmissionCrmConsts.MaxPolicyEvaluationJsonLength);
		FirstDetectedAt = detectedAt.ToUniversalTime();
		LastDetectedAt = FirstDetectedAt;
		Status = AdmissionCrmDataQualityIssueStatus.Open;
	}

	public void Redetect(AdmissionCrmDataQualityIssueSeverity severity, string detailsJson, DateTime detectedAt)
	{
		Severity = severity;
		DetailsJson = Check.NotNullOrWhiteSpace(detailsJson, nameof(detailsJson), AdmissionCrmConsts.MaxPolicyEvaluationJsonLength);
		LastDetectedAt = detectedAt.ToUniversalTime();
		if (Status == AdmissionCrmDataQualityIssueStatus.Resolved)
		{
			Status = AdmissionCrmDataQualityIssueStatus.Open;
			ResolutionReason = null;
			ResolvedByUserId = null;
			ResolvedAt = null;
		}
	}

	public void Acknowledge(Guid? ownerUserId)
	{
		if (Status == AdmissionCrmDataQualityIssueStatus.Resolved)
			throw new BusinessException("AdmissionCrm:DataQualityIssueAlreadyResolved");
		OwnerUserId = ownerUserId;
		Status = AdmissionCrmDataQualityIssueStatus.Acknowledged;
	}

	public void Resolve(bool ignore, string reason, Guid userId, DateTime at)
	{
		ResolutionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		ResolvedByUserId = userId;
		ResolvedAt = at.ToUniversalTime();
		Status = ignore ? AdmissionCrmDataQualityIssueStatus.Ignored : AdmissionCrmDataQualityIssueStatus.Resolved;
	}

	public void ResolveByReconciliation(DateTime at)
	{
		if (Status is AdmissionCrmDataQualityIssueStatus.Resolved or AdmissionCrmDataQualityIssueStatus.Ignored) return;
		ResolutionReason = "The issue was no longer detected by reconciliation.";
		ResolvedByUserId = null;
		ResolvedAt = at.ToUniversalTime();
		Status = AdmissionCrmDataQualityIssueStatus.Resolved;
	}
}
