using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicInquiryWorkflow(
	AdmissionCrmPublicFormWorkflow forms,
	AdmissionCrmPublicCaptureConfiguration captureConfiguration,
	AdmissionCrmStaffEligibilityService staffEligibility,
	AdmissionCrmPublicDuplicateDetector duplicateDetector,
	AdmissionCrmPublicInquiryPersistence persistence,
	AdmissionCrmEnrollmentAutomationCoordinator automation) : ApplicationService, ITransientDependency
{
	public async Task<PublicAdmissionInquirySubmitResultDto> SubmitAsync(
		Guid tenantId,
		Guid templateId,
		PublicAdmissionInquirySubmitDto input,
		string? ipAddress,
		string? userAgent)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var submissionKey = Check.NotNullOrWhiteSpace(
				input.SubmissionKey, nameof(input.SubmissionKey), AdmissionCrmConsts.MaxSubmissionKeyLength);
			var existing = await persistence.GetExistingAsync(templateId, submissionKey);
			if (existing != null)
			{
				return existing;
			}

			var template = await forms.GetActiveTemplateAsync(templateId);
			var cycle = await captureConfiguration.GetCycleConfigurationOrDefaultAsync(template.AcademicYearId);
			var channelSetting = AdmissionCrmChannelSettingsSerializer.GetChannelSetting(
				cycle.ChannelSettingsJson, template.Channel);
			if (!channelSetting.IsEnabled)
			{
				throw new BusinessException("AdmissionCrm:ChannelDisabled").WithData("Channel", template.Channel);
			}

			var fields = AdmissionCrmPublicFormMapper.DeserializeFields(template.FieldsJson);
			var mapped = AdmissionCrmPublicSubmissionMapper.ValidateAndMap(template, fields, input);
			var setup = await captureConfiguration.GetAdmissionYearSetupForCaptureAsync(template.AcademicYearId);
			var targetStandardId = AdmissionCrmPublicSubmissionMapper.ParseGuid(
				AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "TargetStandardId"));
			var routeType = AdmissionCrmPublicCaptureConfiguration.ResolveDefaultPublicRoute(setup);
			if (setup is not null)
			{
				EnsurePublishedCaptureFields(mapped);
				AdmissionCrmPublicCaptureConfiguration.ValidateAdmissionYearScope(setup, targetStandardId, routeType);
			}

			var routingSettings = AdmissionCrmLeadRoutingRuleMatcher.DeserializeSettings(cycle.LeadRoutingRulesJson);
			var trackedOwnerUserId = AdmissionCrmPublicSubmissionMapper.ResolveTrackedOwnerUserId(
				template.Channel, mapped, channelSetting, routingSettings);
			var ownerUserId = trackedOwnerUserId ?? channelSetting.DefaultOwnerUserId;
			var source = await captureConfiguration.ResolvePublicInquirySourceAsync(
				template.AcademicYearId, template.Channel, channelSetting.DefaultInquirySourceId, setup is not null);
			var inquiryType = await captureConfiguration.ResolveDefaultInquiryTypeAsync(
				template.AcademicYearId, setup is not null);
			var lead = CreateLead(
				tenantId, template, mapped, targetStandardId, routeType, source, inquiryType, setup, cycle);
			if (ownerUserId.HasValue &&
				await staffEligibility.IsOwnerEligibleAsync(ownerUserId.Value, template.AcademicYearId))
			{
				lead.AssignOwner(ownerUserId.Value);
			}
			else
			{
				ownerUserId = null;
			}

			var duplicateCount = await duplicateDetector.CountLikelyDuplicatesAsync(lead);
			if (duplicateCount > 0)
			{
				lead.RecordDuplicateContinue(
					"Public form submitted with likely duplicate details. Review before follow-up.");
			}

			var result = await persistence.PersistAsync(
				new AdmissionCrmPublicInquiryPersistenceInput(
					tenantId, lead, template, input, submissionKey, ipAddress, userAgent,
					duplicateCount, ownerUserId, trackedOwnerUserId));
			await automation.TrySendAcknowledgementAsync(
				lead.Id,
				channelSetting.DefaultAcknowledgementTemplateId,
				new Dictionary<string, string> { ["statusLink"] = result.StatusPath });
			await automation.RunJourneyRulesAsync(
				lead.AcademicYearId,
				new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
					LeadId = lead.Id
				});
			return result;
		}
	}

	private AdmissionInquiryLead CreateLead(
		Guid tenantId,
		AdmissionInquiryFormTemplate template,
		IReadOnlyDictionary<string, string?> mapped,
		Guid? targetStandardId,
		AdmissionCrmRouteType? routeType,
		AdmissionCrmInquirySource? source,
		AdmissionCrmInquiryType? inquiryType,
		AdmissionCrmAdmissionYearSetup? setup,
		AdmissionCrmCycleConfiguration cycle)
	{
		var lead = new AdmissionInquiryLead(
			GuidGenerator.Create(), tenantId, template.AcademicYearId, source?.Id, source?.DisplayName,
			template.Channel,
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "CampaignName"),
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "CampusBranchName"),
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "PreferredCommunicationLanguage"),
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "FirstName"),
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "MiddleName"),
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "LastName"),
			AdmissionCrmPublicSubmissionMapper.ParseDateTimeOffset(
				AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "DateOfBirth")),
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "ParentGuardianName"),
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "PrimaryContactNo"),
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "Email"),
			AdmissionCrmPublicSubmissionMapper.GetRequiredValue(
				mapped, "AdmissionIntent", "AdmissionCrm:PublicSubmissionIntentRequired"),
			targetStandardId,
			AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "DesiredClassName"),
			notes: null, Clock.Now, routeType, null, null, null);
		lead.ApplyInquiryType(inquiryType?.Id, inquiryType?.DisplayName);
		lead.ApplyAdmissionYearSetup(
			setup?.Id,
			setup?.Version ?? 0,
			setup?.PublishedSnapshotJson ?? AdmissionCrmPublicCaptureConfiguration.BuildLegacyAdmissionYearSnapshot(cycle));
		lead.UpdateScore(AdmissionCrmLeadScoreCalculator.Calculate(
			lead, AdmissionCrmLeadScoreCalculator.DeserializeRules(cycle.LeadScoringRulesJson)));
		return lead;
	}

	private static void EnsurePublishedCaptureFields(IReadOnlyDictionary<string, string?> mapped)
	{
		if (string.IsNullOrWhiteSpace(AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "ParentGuardianName")))
		{
			throw new BusinessException("AdmissionCrm:ParentGuardianNameRequired");
		}

		if (string.IsNullOrWhiteSpace(AdmissionCrmPublicSubmissionMapper.GetValue(mapped, "PrimaryContactNo")))
		{
			throw new BusinessException("AdmissionCrm:GuardianMobileRequired");
		}
	}
}
