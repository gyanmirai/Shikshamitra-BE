using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp.Timing;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmPolicyMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmPolicyPackDto Map(AdmissionCrmPolicyPack entity, IClock clock) => new()
	{
		Id = entity.Id,
		Code = entity.Code,
		Name = entity.Name,
		Jurisdiction = entity.Jurisdiction,
		Board = entity.Board,
		Version = entity.Version,
		Status = entity.Status,
		Sources = Deserialize<IReadOnlyList<AdmissionCrmPolicySourceDto>>(entity.SourcesJson) ?? [],
		Rules = Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(entity.RulesJson) ?? [],
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveUntil = entity.EffectiveUntil,
		ReviewDueAt = entity.ReviewDueAt,
		SourceReviewOverdue = entity.ReviewDueAt < clock.Now,
		SubmittedByUserId = entity.SubmittedByUserId,
		SubmittedAt = entity.SubmittedAt,
		ApprovedByUserId = entity.ApprovedByUserId,
		ApprovedAt = entity.ApprovedAt,
		PublishedAt = entity.PublishedAt,
		SupersededByPolicyPackId = entity.SupersededByPolicyPackId
	};

	public static AdmissionCrmPolicyAdoptionDto Map(AdmissionCrmPolicyAdoption entity)
	{
		var preview = Deserialize<Dictionary<string, IReadOnlyList<string>>>(entity.ImpactPreviewJson);
		return new AdmissionCrmPolicyAdoptionDto
		{
			Id = entity.Id,
			AcademicYearId = entity.AcademicYearId,
			PolicyPackId = entity.PolicyPackId,
			PolicyPackVersion = entity.PolicyPackVersion,
			Applicability = Deserialize<AdmissionCrmPolicyApplicabilityDto>(entity.ApplicabilityJson) ?? new(),
			Overrides = Deserialize<IReadOnlyList<AdmissionCrmPolicyOverrideDto>>(entity.OverridesJson) ?? [],
			ImpactWarnings = preview?.GetValueOrDefault("warnings") ?? [],
			OverrideReason = entity.OverrideReason,
			EffectiveFrom = entity.EffectiveFrom,
			EffectiveUntil = entity.EffectiveUntil,
			AdoptedByUserId = entity.AdoptedByUserId,
			AdoptedAt = entity.AdoptedAt,
			IsActive = entity.IsActive
		};
	}

	public static AdmissionCrmPolicyEvaluationDto Map(AdmissionCrmPolicyEvaluation entity) => new()
	{
		Id = entity.Id,
		LeadId = entity.LeadId,
		PolicyAdoptionId = entity.PolicyAdoptionId,
		RuleCode = entity.RuleCode,
		RuleType = entity.RuleType,
		Severity = entity.Severity,
		Status = entity.Status,
		InputSnapshot = Deserialize<Dictionary<string, object?>>(entity.InputSnapshotJson) ?? new(),
		EvidenceReferences = Deserialize<IReadOnlyList<string>>(entity.EvidenceJson) ?? [],
		Explanation = entity.Explanation,
		EvaluatedAt = entity.EvaluatedAt,
		ExceptionId = entity.ExceptionId
	};

	public static AdmissionCrmPolicyExceptionDto Map(AdmissionCrmPolicyException entity) => new()
	{
		Id = entity.Id,
		LeadId = entity.LeadId,
		EvaluationId = entity.EvaluationId,
		Status = entity.Status,
		Reason = entity.Reason,
		EvidenceReference = entity.EvidenceReference,
		RequestedByUserId = entity.RequestedByUserId,
		RequestedAt = entity.RequestedAt,
		DecidedByUserId = entity.DecidedByUserId,
		DecidedAt = entity.DecidedAt,
		DecisionReason = entity.DecisionReason,
		ExpiresAt = entity.ExpiresAt
	};

	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	public static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
	public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
}
