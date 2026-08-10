using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmConfigurationImpactAnalyzer
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static IReadOnlyList<string> ChangedFields(AdmissionCrmCycleConfiguration? current,
		UpdateAdmissionCrmCycleConfigurationDto? proposed)
	{
		if (proposed == null) return [];
		if (current == null) return ["CycleConfiguration"];
		var changed = new List<string>();
		Add(changed, "CycleName", current.CycleName, proposed.CycleName);
		Add(changed, "IsActive", current.IsActive, proposed.IsActive);
		Add(changed, "RegistrationFormRequiredBeforeExam", current.RegistrationFormRequiredBeforeExam, proposed.RegistrationFormRequiredBeforeExam);
		Add(changed, "EntranceExamRequired", current.EntranceExamRequired, proposed.EntranceExamRequired);
		Add(changed, "StaleAfterDays", current.StaleAfterDays, proposed.StaleAfterDays);
		Add(changed, "ArchiveAfterDays", current.ArchiveAfterDays, proposed.ArchiveAfterDays);
		Add(changed, "DefaultLeadSlaHours", current.DefaultLeadSlaHours, proposed.DefaultLeadSlaHours);
		Add(changed, "ActivationCancellationWindowDays", current.ActivationCancellationWindowDays, proposed.ActivationCancellationWindowDays);
		Add(changed, "DefaultExamPassingPercentage", current.DefaultExamPassingPercentage, proposed.DefaultExamPassingPercentage);
		Add(changed, "MeritRankingMode", current.MeritRankingMode, proposed.MeritRankingMode);
		Add(changed, "ExamReadinessFields", current.ExamReadinessFields, proposed.ExamReadinessFields);
		Add(changed, "LeadScoringRules", current.LeadScoringRulesJson, AdmissionCrmLeadScoreCalculator.SerializeRules(proposed.LeadScoringRules));
		Add(changed, "LeadRoutingSettings", current.LeadRoutingRulesJson, AdmissionCrmLeadRoutingRuleMatcher.SerializeSettings(proposed.LeadRoutingSettings));
		Add(changed, "JourneyRules", current.JourneyRulesJson, AdmissionCrmJourneyRuleSerializer.SerializeRules(proposed.JourneyRules));
		Add(changed, "ApplicantChecklist", current.ApplicantChecklistJson, AdmissionCrmApplicantChecklistSerializer.SerializeItems(proposed.ApplicantChecklist));
		Add(changed, "ChannelSettings", current.ChannelSettingsJson, AdmissionCrmChannelSettingsSerializer.SerializeSettings(proposed.ChannelSettings));
		Add(changed, "SlaPolicy", current.SlaPolicyJson, JsonSerializer.Serialize(proposed.SlaPolicy, JsonOptions));
		Add(changed, "Notes", current.Notes ?? string.Empty, proposed.Notes ?? string.Empty);
		return changed;
	}

	public static IReadOnlyList<string> ChangedFields(AdmissionCrmPipelineStage? current,
		UpdateAdmissionCrmPipelineStageDto? proposed)
	{
		if (proposed == null) return [];
		if (current == null) return ["PipelineStage"];
		var changed = new List<string>();
		Add(changed, "StageName", current.StageName, proposed.StageName);
		Add(changed, "DisplayOrder", current.DisplayOrder, proposed.DisplayOrder);
		Add(changed, "IsEnabled", current.IsEnabled, proposed.IsEnabled);
		Add(changed, "ColorHex", current.ColorHex, proposed.ColorHex ?? current.ColorHex);
		Add(changed, "SlaTargetHours", current.SlaTargetHours, proposed.SlaTargetHours ?? current.SlaTargetHours);
		Add(changed, "OwnerResponsibility", current.OwnerResponsibility, proposed.OwnerResponsibility ?? current.OwnerResponsibility);
		if (proposed.AllowedNextStages != null)
			Add(changed, "AllowedNextStages", current.AllowedNextStagesJson, JsonSerializer.Serialize(proposed.AllowedNextStages, JsonOptions));
		return changed;
	}

	public static void Validate(PreviewAdmissionCrmConfigurationImpactDto input)
	{
		if (input.Kind.HasValue != !string.IsNullOrWhiteSpace(input.Code))
			throw new BusinessException("AdmissionCrm:ConfigurationReferenceKindAndCodeRequiredTogether");
		if (input.PipelineStageId.HasValue != (input.ProposedPipelineStage != null))
			throw new BusinessException("AdmissionCrm:ConfigurationStageIdAndProposalRequiredTogether");
		if (input.PipelineStageId == Guid.Empty) throw new BusinessException("AdmissionCrm:PipelineStageRequired");
		if (input.PipelineStageId.HasValue && input.Kind.HasValue)
			throw new BusinessException("AdmissionCrm:ConfigurationStageReferenceIsDerived");
		if (!input.Kind.HasValue && input.ProposedCycleConfiguration == null && !input.PipelineStageId.HasValue)
			throw new BusinessException("AdmissionCrm:ConfigurationImpactInputRequired");
	}

	public static object InputSignature(AdmissionCrmConfigurationReferenceKind? kind, string? code,
		UpdateAdmissionCrmCycleConfigurationDto? cycle, Guid? stageId, UpdateAdmissionCrmPipelineStageDto? stage) => new
	{
		Kind = kind, Code = kind.HasValue ? AdmissionCrmConfigurationReferenceWorkflow.Normalize(code!) : null,
		ProposedCycle = cycle == null ? null : new { cycle.CycleName, cycle.IsActive, cycle.RegistrationFormRequiredBeforeExam,
			cycle.EntranceExamRequired, cycle.StaleAfterDays, cycle.ArchiveAfterDays, cycle.DefaultLeadSlaHours,
			cycle.SlaPolicy, cycle.ActivationCancellationWindowDays, cycle.DefaultExamPassingPercentage,
			cycle.MeritRankingMode, cycle.ExamReadinessFields, cycle.LeadScoringRules, cycle.LeadRoutingSettings,
			cycle.JourneyRules, cycle.ApplicantChecklist, cycle.ChannelSettings, cycle.Notes },
		PipelineStageId = stageId,
		ProposedStage = stage == null ? null : new { stage.StageName, stage.DisplayOrder, stage.IsEnabled,
			stage.ColorHex, stage.SlaTargetHours, stage.OwnerResponsibility, stage.AllowedNextStages }
	};

	public static IReadOnlyList<string> Warnings(int applicantCount, int referenceCount,
		IReadOnlyCollection<string> changed, AdmissionCrmConfigurationReferenceKind? kind, string? code)
	{
		var warnings = new List<string>();
		if (changed.Count > 0 && applicantCount > 0)
			warnings.Add($"{applicantCount} current-year applicant record(s) may be affected by the proposed cycle configuration.");
		if (changed.Contains("EntranceExamRequired"))
			warnings.Add("Changing entrance-exam requirements can alter registration gates and applicant actions.");
		if (changed.Contains("JourneyRules") || changed.Contains("LeadRoutingSettings") || changed.Contains("ChannelSettings"))
			warnings.Add("Journey, routing, or channel changes can alter future ownership and communications.");
		if (kind.HasValue && referenceCount > 0)
			warnings.Add($"{referenceCount} reference(s) to {kind.Value} code '{AdmissionCrmConfigurationReferenceWorkflow.Normalize(code!)}' must be reviewed before changing or retiring it.");
		if (kind.HasValue && referenceCount == 0)
			warnings.Add($"No current-year references were found for {kind.Value} code '{AdmissionCrmConfigurationReferenceWorkflow.Normalize(code!)}'.");
		return warnings;
	}

	private static void Add<T>(ICollection<string> changed, string field, T current, T proposed)
	{
		if (!EqualityComparer<T>.Default.Equals(current, proposed)) changed.Add(field);
	}
}
