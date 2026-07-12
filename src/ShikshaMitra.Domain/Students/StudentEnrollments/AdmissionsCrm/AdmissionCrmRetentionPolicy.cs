using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRetentionPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public AdmissionCrmRetentionCategory Category { get; private set; }
	public AdmissionCrmRetentionAction Action { get; private set; }
	public int RetainDays { get; private set; }
	public string ScopeJson { get; private set; } = "{}";
	public string AuthorityNote { get; private set; } = string.Empty;
	public AdmissionCrmGovernedConfigurationStatus Status { get; private set; }
	public Guid? SubmittedByUserId { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? PublishedAt { get; private set; }

	protected AdmissionCrmRetentionPolicy() { }

	public AdmissionCrmRetentionPolicy(Guid id, Guid? tenantId, Guid academicYearId, string code,
		string name, AdmissionCrmRetentionCategory category, AdmissionCrmRetentionAction action,
		int retainDays, string scopeJson, string authorityNote) : base(id)
	{
		if (retainDays is < 0 or > 36500) throw new BusinessException("AdmissionCrm:InvalidRetentionPeriod");
		TenantId = tenantId; AcademicYearId = academicYearId;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxPolicyCodeLength).ToUpperInvariant();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		Category = category; Action = action; RetainDays = retainDays;
		ScopeJson = ValidateObject(scopeJson); AuthorityNote = Check.NotNullOrWhiteSpace(authorityNote, nameof(authorityNote), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmGovernedConfigurationStatus.Draft;
	}

	public void Submit(Guid userId)
	{
		if (Status != AdmissionCrmGovernedConfigurationStatus.Draft) throw new BusinessException("AdmissionCrm:RetentionPolicyNotDraft");
		SubmittedByUserId = userId; Status = AdmissionCrmGovernedConfigurationStatus.ApprovalPending;
	}

	public void Publish(Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmGovernedConfigurationStatus.ApprovalPending) throw new BusinessException("AdmissionCrm:RetentionPolicyNotAwaitingApproval");
		if (SubmittedByUserId == userId) throw new BusinessException("AdmissionCrm:DistinctRetentionPolicyApproverRequired");
		ApprovedByUserId = userId; PublishedAt = at.ToUniversalTime(); Status = AdmissionCrmGovernedConfigurationStatus.Published;
	}

	private static string ValidateObject(string json)
	{
		var value = Check.NotNullOrWhiteSpace(json, nameof(json), AdmissionCrmConsts.MaxActivationStepsJsonLength);
		using var document = JsonDocument.Parse(value);
		if (document.RootElement.ValueKind != JsonValueKind.Object) throw new BusinessException("AdmissionCrm:InvalidRetentionScope");
		return value;
	}
}
