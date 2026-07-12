using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyAdoption : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid PolicyPackId { get; private set; }
	public int PolicyPackVersion { get; private set; }
	public string ApplicabilityJson { get; private set; } = "{}";
	public string OverridesJson { get; private set; } = "[]";
	public string ImpactPreviewJson { get; private set; } = "{}";
	public string? OverrideReason { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveUntil { get; private set; }
	public Guid AdoptedByUserId { get; private set; }
	public DateTime AdoptedAt { get; private set; }
	public bool IsActive { get; private set; }

	protected AdmissionCrmPolicyAdoption() { }

	public AdmissionCrmPolicyAdoption(Guid id, Guid? tenantId, Guid academicYearId, Guid policyPackId,
		int policyPackVersion, string applicabilityJson, string overridesJson, string impactPreviewJson,
		string? overrideReason, DateTime effectiveFrom, DateTime? effectiveUntil, Guid adoptedByUserId,
		DateTime adoptedAt) : base(id)
	{
		if (policyPackVersion <= 0) throw new BusinessException("AdmissionCrm:InvalidPolicyPackVersion");
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		PolicyPackId = policyPackId;
		PolicyPackVersion = policyPackVersion;
		ApplicabilityJson = Validate(applicabilityJson, JsonValueKind.Object, "Applicability");
		OverridesJson = Validate(overridesJson, JsonValueKind.Array, "Overrides");
		ImpactPreviewJson = Validate(impactPreviewJson, JsonValueKind.Object, "ImpactPreview");
		if (overridesJson != "[]" && string.IsNullOrWhiteSpace(overrideReason))
			throw new BusinessException("AdmissionCrm:PolicyOverrideReasonRequired");
		OverrideReason = Normalize(overrideReason);
		EffectiveFrom = effectiveFrom.ToUniversalTime();
		EffectiveUntil = effectiveUntil?.ToUniversalTime();
		if (EffectiveUntil.HasValue && EffectiveUntil <= EffectiveFrom)
			throw new BusinessException("AdmissionCrm:InvalidPolicyAdoptionEffectivePeriod");
		AdoptedByUserId = adoptedByUserId;
		AdoptedAt = adoptedAt.ToUniversalTime();
		IsActive = true;
	}

	public void Deactivate() => IsActive = false;

	private static string Validate(string json, JsonValueKind kind, string field)
	{
		var value = Check.NotNullOrWhiteSpace(json, field, AdmissionCrmConsts.MaxPolicyRulesJsonLength);
		using var document = JsonDocument.Parse(value);
		if (document.RootElement.ValueKind != kind)
			throw new BusinessException("AdmissionCrm:InvalidPolicyAdoptionJson").WithData("Field", field);
		return value;
	}

	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value)
		? null : Check.Length(value.Trim(), nameof(value), AdmissionCrmConsts.MaxNotesLength);
}
