using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmApplicationStagePolicyMapper
{
	internal static AdmissionCrmApplicationStagePolicyDto Build(
		AdmissionCrmCycleConfiguration configuration,
		int applicantCount)
	{
		var policy = AdmissionCrmApplicationStagePolicyNormalizer.NormalizeStored(configuration);
		policy.EntranceExamRequired = configuration.EntranceExamRequired;
		policy.JourneyLocked = configuration.JourneyLocked;
		policy.ApplicantCount = applicantCount;
		policy.ImpactFingerprint = Fingerprint(
			configuration,
			policy.Stages,
			applicantCount);
		return policy;
	}

	internal static AdmissionCrmApplicationStagePolicyImpactDto BuildImpact(
		AdmissionCrmCycleConfiguration configuration,
		AdmissionCrmApplicationStagePolicyDto candidate,
		int applicantCount)
	{
		var current = AdmissionCrmApplicationStagePolicyNormalizer.NormalizeStored(configuration);
		var warnings = new List<string>();
		if (applicantCount > 0)
		{
			warnings.Add("Existing applicants stay in their current CRM stage; this policy changes the cycle setup summary and future stage guidance only.");
		}

		if (current.Stages.Any(stage =>
			stage.IsExamStage &&
			stage.IsEnabled &&
			candidate.Stages.Any(next => next.StageKey == stage.StageKey && !next.IsEnabled)))
		{
			warnings.Add("Exam stages will be hidden from the configured journey because this admission year does not require an entrance exam.");
		}

		if (current.Stages.Any(stage =>
			candidate.Stages.Any(next =>
				next.StageKey == stage.StageKey &&
				next.DisplayOrder != stage.DisplayOrder)))
		{
			warnings.Add("Stage ordering changes how administrators read this admission year journey; it does not migrate applicant history.");
		}

		return new AdmissionCrmApplicationStagePolicyImpactDto
		{
			ApplicantCount = applicantCount,
			ImpactFingerprint = Fingerprint(
				configuration,
				candidate.Stages,
				applicantCount),
			RequiresConfirmation = applicantCount > 0 || configuration.IsActive,
			Warnings = warnings
		};
	}

	internal static string Serialize(AdmissionCrmApplicationStagePolicyDto policy)
	{
		return JsonSerializer.Serialize(
			new AdmissionCrmApplicationStagePolicyDto
			{
				Version = policy.Version,
				EntranceExamRequired = policy.EntranceExamRequired,
				JourneyLocked = policy.JourneyLocked,
				Stages = policy.Stages
			},
			AdmissionCrmSetupFormat.JsonOptions);
	}

	internal static string Fingerprint(
		AdmissionCrmCycleConfiguration configuration,
		IReadOnlyList<AdmissionCrmApplicationStagePolicyItemDto> stages,
		int applicantCount)
	{
		var payload = JsonSerializer.Serialize(new
		{
			configuration.AcademicYearId,
			configuration.IsActive,
			configuration.JourneyLocked,
			configuration.EntranceExamRequired,
			applicantCount,
			stages = stages
				.OrderBy(stage => stage.DisplayOrder)
				.Select(stage => new { stage.StageKey, stage.DisplayOrder, stage.IsEnabled })
		}, AdmissionCrmSetupFormat.JsonOptions);
		return AdmissionCrmSetupFormat.Hash(payload);
	}
}