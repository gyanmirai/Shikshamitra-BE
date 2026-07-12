using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyPack : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public string Jurisdiction { get; private set; } = string.Empty;
	public string Board { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmPolicyPackStatus Status { get; private set; }
	public string SourcesJson { get; private set; } = "[]";
	public string RulesJson { get; private set; } = "[]";
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveUntil { get; private set; }
	public DateTime ReviewDueAt { get; private set; }
	public Guid? SubmittedByUserId { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? SupersededByPolicyPackId { get; private set; }

	protected AdmissionCrmPolicyPack() { }

	public AdmissionCrmPolicyPack(Guid id, Guid? tenantId, string code, string name, string jurisdiction,
		string board, int version, string sourcesJson, string rulesJson, DateTime effectiveFrom,
		DateTime? effectiveUntil, DateTime reviewDueAt) : base(id)
	{
		if (version <= 0) throw new BusinessException("AdmissionCrm:InvalidPolicyPackVersion");
		TenantId = tenantId;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxPolicyCodeLength).ToUpperInvariant();
		Version = version;
		Status = AdmissionCrmPolicyPackStatus.Draft;
		UpdateDraft(name, jurisdiction, board, sourcesJson, rulesJson, effectiveFrom, effectiveUntil, reviewDueAt);
	}

	public void UpdateDraft(string name, string jurisdiction, string board, string sourcesJson,
		string rulesJson, DateTime effectiveFrom, DateTime? effectiveUntil, DateTime reviewDueAt)
	{
		if (Status != AdmissionCrmPolicyPackStatus.Draft)
			throw new BusinessException("AdmissionCrm:OnlyDraftPolicyPackCanBeChanged");
		var from = effectiveFrom.ToUniversalTime();
		var until = effectiveUntil?.ToUniversalTime();
		var review = reviewDueAt.ToUniversalTime();
		if (until.HasValue && until <= from || review < from)
			throw new BusinessException("AdmissionCrm:InvalidPolicyPackEffectivePeriod");
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		Jurisdiction = Check.NotNullOrWhiteSpace(jurisdiction, nameof(jurisdiction), AdmissionCrmConsts.MaxPolicyJurisdictionLength);
		Board = Check.NotNullOrWhiteSpace(board, nameof(board), AdmissionCrmConsts.MaxPolicyJurisdictionLength);
		SourcesJson = ValidateArray(sourcesJson, AdmissionCrmConsts.MaxPolicySourceJsonLength, "Sources");
		RulesJson = ValidateArray(rulesJson, AdmissionCrmConsts.MaxPolicyRulesJsonLength, "Rules");
		EffectiveFrom = from;
		EffectiveUntil = until;
		ReviewDueAt = review;
	}

	public void Submit(Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmPolicyPackStatus.Draft) throw new BusinessException("AdmissionCrm:PolicyPackNotDraft");
		SubmittedByUserId = userId;
		SubmittedAt = at.ToUniversalTime();
		Status = AdmissionCrmPolicyPackStatus.ReviewPending;
	}

	public void Approve(Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmPolicyPackStatus.ReviewPending) throw new BusinessException("AdmissionCrm:PolicyPackNotAwaitingReview");
		if (SubmittedByUserId == userId) throw new BusinessException("AdmissionCrm:DistinctPolicyPackApproverRequired");
		ApprovedByUserId = userId;
		ApprovedAt = at.ToUniversalTime();
		Status = AdmissionCrmPolicyPackStatus.Approved;
	}

	public void Publish(DateTime at)
	{
		if (Status != AdmissionCrmPolicyPackStatus.Approved) throw new BusinessException("AdmissionCrm:ApprovedPolicyPackRequired");
		PublishedAt = at.ToUniversalTime();
		Status = AdmissionCrmPolicyPackStatus.Published;
	}

	public void Supersede(Guid replacementId)
	{
		if (Status != AdmissionCrmPolicyPackStatus.Published) throw new BusinessException("AdmissionCrm:PublishedPolicyPackRequired");
		SupersededByPolicyPackId = replacementId;
		Status = AdmissionCrmPolicyPackStatus.Superseded;
	}

	private static string ValidateArray(string json, int maxLength, string field)
	{
		var value = Check.NotNullOrWhiteSpace(json, field, maxLength);
		using var document = JsonDocument.Parse(value);
		if (document.RootElement.ValueKind != JsonValueKind.Array)
			throw new BusinessException("AdmissionCrm:InvalidPolicyPackJson").WithData("Field", field);
		return value;
	}
}
