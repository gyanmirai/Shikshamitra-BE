using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadCaptureWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmStaffEligibilityService staffEligibility,
	AdmissionCrmLeadAccess access,
	AdmissionCrmLeadCaptureValidator validator,
	AdmissionCrmLeadConfigurationReader configuration,
	AdmissionCrmLeadDuplicateDetector duplicateDetector,
	AdmissionCrmLeadTimelineWriter timeline,
	AdmissionCrmJourneyWorkflow journey) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmLeadDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmLeadDto input)
	{
		await access.EnsureAcademicYearExistsAsync(academicYearId);
		await access.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(CreateAsync));
		AdmissionCrmLeadCaptureRules.EnsureStaffApplicantName(input);
		if (input.TargetStandardId.HasValue)
		{
			await access.EnsureCanAccessStandardAsync(
				input.TargetStandardId.Value,
				nameof(CreateAsync));
			await validator.EnsureAcademicPlacementAsync(academicYearId, input);
		}
		var cycle = await configuration.GetCycleConfigurationOrDefaultAsync(academicYearId);
		var admissionYearSetup = await access.GetAdmissionYearSetupForCaptureAsync(academicYearId);
		if (admissionYearSetup is not null)
		{
			AdmissionCrmLeadCaptureRules.EnsurePublishedAdmissionYearCaptureFields(input);
		}
		var channelSetting = AdmissionCrmChannelSettingsSerializer.GetChannelSetting(
			cycle.ChannelSettingsJson,
			input.Channel);
		if (!channelSetting.IsEnabled)
		{
			throw new BusinessException("AdmissionCrm:ChannelDisabled")
				.WithData("Channel", input.Channel);
		}

		var inquirySourceId = input.InquirySourceId ?? channelSetting.DefaultInquirySourceId;
		await access.EnsureCanUseInquirySourceAsync(
			inquirySourceId,
			academicYearId,
			input.Channel,
			nameof(CreateAsync));

		var scoringRules = AdmissionCrmLeadScoreCalculator.DeserializeRules(cycle.LeadScoringRulesJson);
		var routingSettings = AdmissionCrmLeadRoutingRuleMatcher.DeserializeSettings(cycle.LeadRoutingRulesJson);
		var inquirySourceSnapshot = await configuration.GetInquirySourceSnapshotAsync(inquirySourceId);
		var intendedRouteDefinition = await validator.ResolveIntendedRouteDefinitionAsync(
			academicYearId,
			input,
			admissionYearSetup is not null);
		var inquiryType = await validator.ResolveInquiryTypeAsync(academicYearId, input.InquiryTypeId);
		if (admissionYearSetup is not null && !inquirySourceId.HasValue)
		{
			throw new BusinessException("AdmissionCrm:InquirySourceRequired")
				.WithData("AcademicYearId", academicYearId);
		}
		if (admissionYearSetup is not null && inquiryType is null)
		{
			throw new BusinessException("AdmissionCrm:InquiryTypeRequired")
				.WithData("AcademicYearId", academicYearId);
		}
		AdmissionCrmLeadCaptureRules.ValidateAdmissionYearScope(
			admissionYearSetup,
			input.TargetStandardId,
			intendedRouteDefinition?.RouteType ?? input.IntendedRouteType);
		var duplicates = await duplicateDetector.FindDuplicatesAsync(
			academicYearId,
			AdmissionCrmLeadDuplicateMatcher.NormalizePhone(input.PrimaryContactNo),
			string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant(),
			AdmissionCrmDuplicateProfileProbe.FromCreateInput(input),
			excludeLeadId: null);
		if (duplicates.Count > 0)
		{
			if (string.IsNullOrWhiteSpace(input.DuplicateContinueReason))
			{
				throw new BusinessException("AdmissionCrm:LikelyDuplicateRequiresReason")
					.WithData("DuplicateLeadIds", string.Join(",", duplicates.Select(item => item.LeadId)));
			}

			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MergeDuplicates);
		}

		var lead = new AdmissionInquiryLead(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			inquirySourceId,
			inquirySourceSnapshot,
			input.Channel,
			input.CampaignName,
			input.CampusBranchName,
			input.PreferredCommunicationLanguage,
			input.FirstName,
			input.MiddleName,
			input.LastName,
			input.DateOfBirth,
			input.ParentGuardianName,
			input.PrimaryContactNo,
			input.Email,
			input.AdmissionIntent,
			input.TargetStandardId,
			input.DesiredClassName,
			input.Notes,
			Clock.Now,
			intendedRouteDefinition?.RouteType ?? input.IntendedRouteType,
			intendedRouteDefinition?.Id,
			intendedRouteDefinition?.Version,
			intendedRouteDefinition?.Name);
		lead.ApplyInquiryType(inquiryType?.Id, inquiryType?.DisplayName);
		if (admissionYearSetup is null)
			throw new BusinessException("AdmissionCrm:PublishedAdmissionYearSetupRequired");
		lead.ApplyAdmissionYearSetup(
			admissionYearSetup.Id,
			admissionYearSetup.Version,
			admissionYearSetup.PublishedSnapshotJson!);
		if (duplicates.Count > 0)
		{
			lead.RecordDuplicateContinue(input.DuplicateContinueReason!);
		}
		await access.EnsureCanAccessAdmissionLeadAsync(lead, nameof(CreateAsync));
		lead.UpdateScore(AdmissionCrmLeadScoreCalculator.Calculate(lead, scoringRules));
		IReadOnlyCollection<AdmissionInquiryLead> existingLeads = Array.Empty<AdmissionInquiryLead>();
		if (routingSettings.IsEnabled && routingSettings.OwnerPoolUserIds.Count > 0)
		{
			var leadQueryable = await leadRepository.GetQueryableAsync();
			existingLeads = await AsyncExecuter.ToListAsync(leadQueryable.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == academicYearId));
		}

		var routedOwnerUserId = AdmissionCrmLeadRoutingRuleMatcher.ResolveOwner(
			lead,
			routingSettings,
			existingLeads) ?? channelSetting.DefaultOwnerUserId ?? admissionYearSetup?.DefaultOwnerUserId;
		if (routedOwnerUserId.HasValue &&
			await staffEligibility.IsOwnerEligibleAsync(routedOwnerUserId.Value, academicYearId))
		{
			lead.AssignOwner(routedOwnerUserId.Value);
		}
		else
		{
			routedOwnerUserId = null;
		}

		await leadRepository.InsertAsync(lead, autoSave: true);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.LeadCreated,
			"Lead created",
			$"Channel: {AdmissionCrmQuickInquiryOptionsWorkflow.GetInquiryChannelLabel(lead.Channel)}. Intent: {lead.AdmissionIntent}" +
			(lead.IntendedRouteDefinitionId.HasValue
				? $". Intended route: {lead.IntendedRouteNameSnapshot} v{lead.IntendedRouteDefinitionVersion}"
				: string.Empty),
			relatedEntityId: lead.Id);
		if (routedOwnerUserId.HasValue)
		{
			await timeline.AddTimelineEventAsync(
				lead,
				AdmissionCrmTimelineEventType.OwnerAssigned,
				"Owner assigned automatically",
				$"Owner user: {routedOwnerUserId}",
				relatedEntityId: routedOwnerUserId);
		}

		if (duplicates.Count > 0)
		{
			await timeline.AddTimelineEventAsync(
				lead,
				AdmissionCrmTimelineEventType.DuplicateContinued,
				"Duplicate continued",
				input.DuplicateContinueReason!,
				relatedEntityId: lead.Id);
		}

		await journey.TrySendAcknowledgementAsync(
			lead.Id,
			channelSetting.DefaultAcknowledgementTemplateId);
		await journey.RunAsync(
			academicYearId,
			new RunAdmissionCrmJourneyRulesDto
			{
				Trigger = AdmissionCrmJourneyTrigger.LeadCreated,
				LeadId = lead.Id
			});

		return AdmissionCrmLeadMapper.ToDto(lead, duplicates);
	}

}
