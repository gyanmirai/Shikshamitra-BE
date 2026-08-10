using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmApplicationStagePolicyNormalizer
{
	internal static AdmissionCrmApplicationStagePolicyDto NormalizeStored(
		AdmissionCrmCycleConfiguration configuration)
	{
		AdmissionCrmApplicationStagePolicyDto? stored = null;
		if (!string.IsNullOrWhiteSpace(configuration.ApplicationStagePolicyJson) &&
			configuration.ApplicationStagePolicyJson.Trim() != "{}")
		{
			stored = JsonSerializer.Deserialize<AdmissionCrmApplicationStagePolicyDto>(
				configuration.ApplicationStagePolicyJson,
				AdmissionCrmSetupFormat.JsonOptions);
		}

		var requested = stored?.Stages.Select(stage => new UpdateAdmissionCrmApplicationStagePolicyItemDto
		{
			StageKey = stage.StageKey,
			DisplayOrder = stage.DisplayOrder,
			IsEnabled = stage.IsEnabled
		}).ToList() ?? BuildDefault(configuration.EntranceExamRequired);
		return Normalize(configuration, new UpdateAdmissionCrmApplicationStagePolicyDto { Stages = requested });
	}

	internal static AdmissionCrmApplicationStagePolicyDto Normalize(
		AdmissionCrmCycleConfiguration configuration,
		UpdateAdmissionCrmApplicationStagePolicyDto input)
	{
		var definitions = ApplicationStageDefinitions.ToDictionary(stage => stage.StageKey);
		if (input.Stages.Count != definitions.Count)
		{
			throw new BusinessException("AdmissionCrm:ApplicationStagePolicyIncomplete");
		}

		var duplicateStage = input.Stages.GroupBy(stage => stage.StageKey).FirstOrDefault(group => group.Count() > 1);
		if (duplicateStage is not null)
		{
			throw new BusinessException("AdmissionCrm:DuplicateApplicationStage")
				.WithData("StageKey", duplicateStage.Key);
		}

		var duplicateOrder = input.Stages.GroupBy(stage => stage.DisplayOrder).FirstOrDefault(group => group.Count() > 1);
		if (duplicateOrder is not null)
		{
			throw new BusinessException("AdmissionCrm:DuplicateApplicationStageOrder")
				.WithData("DisplayOrder", duplicateOrder.Key);
		}

		var items = new List<AdmissionCrmApplicationStagePolicyItemDto>();
		foreach (var requested in input.Stages.OrderBy(stage => stage.DisplayOrder))
		{
			if (!definitions.TryGetValue(requested.StageKey, out var definition))
			{
				throw new BusinessException("AdmissionCrm:UnsupportedApplicationStage")
					.WithData("StageKey", requested.StageKey);
			}

			if (requested.DisplayOrder <= 0)
			{
				throw new BusinessException("AdmissionCrm:InvalidApplicationStageOrder")
					.WithData("StageKey", requested.StageKey)
					.WithData("DisplayOrder", requested.DisplayOrder);
			}

			if (definition.IsRequired && !definition.IsExamStage && !requested.IsEnabled)
			{
				throw new BusinessException("AdmissionCrm:RequiredApplicationStageDisabled")
					.WithData("StageKey", requested.StageKey);
			}

			if (configuration.EntranceExamRequired && definition.IsExamStage && !requested.IsEnabled)
			{
				throw new BusinessException("AdmissionCrm:RequiredApplicationStageDisabled")
					.WithData("StageKey", requested.StageKey);
			}

			items.Add(new AdmissionCrmApplicationStagePolicyItemDto
			{
				StageKey = definition.StageKey,
				DisplayName = definition.DisplayName,
				DisplayOrder = requested.DisplayOrder,
				IsEnabled = definition.IsExamStage && !configuration.EntranceExamRequired ? false : requested.IsEnabled,
				IsRequired = definition.IsRequired || (configuration.EntranceExamRequired && definition.IsExamStage),
				IsSystemLocked = !definition.IsExamStage,
				IsExamStage = definition.IsExamStage,
				IsOperationallyBacked = definition.SupportedOperationalStages.Count > 0,
				SupportedOperationalStages = definition.SupportedOperationalStages,
				ReadinessNote = definition.ReadinessNote
			});
		}

		return new AdmissionCrmApplicationStagePolicyDto
		{
			Version = 1,
			EntranceExamRequired = configuration.EntranceExamRequired,
			JourneyLocked = configuration.JourneyLocked,
			Stages = items.OrderBy(stage => stage.DisplayOrder).ToList()
		};
	}

	internal static List<UpdateAdmissionCrmApplicationStagePolicyItemDto> BuildDefault(
		bool entranceExamRequired)
	{
		return ApplicationStageDefinitions.Select((definition, index) => new UpdateAdmissionCrmApplicationStagePolicyItemDto
		{
			StageKey = definition.StageKey,
			DisplayOrder = index + 1,
			IsEnabled = !definition.IsExamStage || entranceExamRequired
		}).ToList();
	}

	private sealed record ApplicationStageDefinition(
		AdmissionCrmApplicationStageKey StageKey,
		string DisplayName,
		bool IsRequired,
		bool IsExamStage,
		IReadOnlyList<AdmissionInquiryStage> SupportedOperationalStages,
		string ReadinessNote);

	private static readonly IReadOnlyList<ApplicationStageDefinition> ApplicationStageDefinitions =
	[
		new(
			AdmissionCrmApplicationStageKey.Inquiry,
			"Inquiry",
			true,
			false,
			[AdmissionInquiryStage.NewInquiry],
			"Operationally backed by the current admissions CRM inquiry stage."),
		new(
			AdmissionCrmApplicationStageKey.QualificationFollowUp,
			"Qualification / Follow-up",
			true,
			false,
			[AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.ExamReady],
			"Operationally backed by follow-up and exam-readiness stages."),
		new(
			AdmissionCrmApplicationStageKey.ExamScheduling,
			"Exam Scheduling",
			false,
			true,
			[AdmissionInquiryStage.ExamScheduled],
			"Operationally backed when the cycle requires an entrance exam."),
		new(
			AdmissionCrmApplicationStageKey.ExamResultEligibility,
			"Exam Result / Eligibility",
			false,
			true,
			[AdmissionInquiryStage.ExamCompleted, AdmissionInquiryStage.Eligible, AdmissionInquiryStage.Waitlisted, AdmissionInquiryStage.Rejected],
			"Operationally backed by exam result and eligibility CRM stages."),
		new(
			AdmissionCrmApplicationStageKey.RegistrationDataEntry,
			"Registration / Data Entry",
			true,
			false,
			[],
			"Policy-only in this phase; detailed registration and assisted data entry are later phases."),
		new(
			AdmissionCrmApplicationStageKey.DocumentsVerification,
			"Documents / Verification",
			true,
			false,
			[],
			"Policy-only in this phase; document checklist execution remains in existing settings."),
		new(
			AdmissionCrmApplicationStageKey.FeeVerification,
			"Fee Verification",
			true,
			false,
			[],
			"Policy-only in this phase; no Finance integration is introduced."),
		new(
			AdmissionCrmApplicationStageKey.ClassPlacement,
			"Class Placement",
			true,
			false,
			[],
			"Policy-only in this phase; Class Placement Plan integration is later."),
		new(
			AdmissionCrmApplicationStageKey.AdmissionConfirmation,
			"Admission Confirmation",
			true,
			false,
			[],
			"Policy-only in this phase; confirmation package workflow is later."),
		new(
			AdmissionCrmApplicationStageKey.Activation,
			"Activation",
			true,
			false,
			[],
			"Policy-only in this phase; student record and GR workflow are later."),
		new(
			AdmissionCrmApplicationStageKey.Closed,
			"Closed",
			true,
			false,
			[AdmissionInquiryStage.Archived, AdmissionInquiryStage.Stale, AdmissionInquiryStage.Rejected],
			"Operationally backed by terminal CRM stages.")
	];
}