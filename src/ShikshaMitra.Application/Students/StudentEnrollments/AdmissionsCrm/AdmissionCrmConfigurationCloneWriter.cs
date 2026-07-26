using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConfigurationCloneWriter(
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
	IRepository<AdmissionCrmInquirySource, Guid> inquirySourceRepository,
	IRepository<AdmissionInquiryFormTemplate, Guid> inquiryFormRepository,
	IRepository<AdmissionCrmMessageTemplate, Guid> messageTemplateRepository,
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	IRepository<AdmissionCrmRegistrationTemplate, Guid> registrationTemplateRepository,
	IRepository<AdmissionCrmFormNumberSeries, Guid> formNumberSeriesRepository,
	IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> documentChecklistRepository,
	IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
	IRepository<AdmissionCrmFeePolicy, Guid> feePolicyRepository,
	IRepository<AdmissionCrmRouteDefinition, Guid> routeDefinitionRepository,
	IRepository<AdmissionCrmPolicyAdoption, Guid> policyAdoptionRepository,
	IRepository<AdmissionCrmNumberSeries, Guid> numberSeriesRepository,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	ICurrentUser currentUser,
	IClock clock) : ITransientDependency
{
	internal const string CloneSourceAcademicYearProperty =
		"AdmissionsCrmConfigurationCloneSourceAcademicYearId";
	internal const string ClonePreviewFingerprintProperty =
		"AdmissionsCrmConfigurationClonePreviewFingerprint";

	internal async Task WriteAsync(
		AdmissionCrmConfigurationSnapshot source,
		AcademicYear targetAcademicYear,
		string previewFingerprint,
		IReadOnlyDictionary<Guid, Guid> hierarchyIdMap)
	{
		if (source.Cycle == null)
		{
			throw new InvalidOperationException("A source admission cycle is required for configuration cloning.");
		}

		var idMap = BuildIdMap(source, hierarchyIdMap);
		var targetAcademicYearId = targetAcademicYear.Id;
		var dateOffset = targetAcademicYear.StartDate - source.AcademicYear.StartDate;

		var cycle = CloneCycle(source.Cycle, targetAcademicYearId, idMap);
		cycle.SetProperty(CloneSourceAcademicYearProperty, source.AcademicYear.Id.ToString("D"));
		cycle.SetProperty(ClonePreviewFingerprintProperty, previewFingerprint);
		await cycleRepository.InsertAsync(cycle, autoSave: false);

		foreach (var sourceStage in source.PipelineStages.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id))
		{
			var stage = sourceStage.IsSystem
				? new AdmissionCrmPipelineStage(
					idMap[sourceStage.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceStage.SystemStage,
					sourceStage.StageName,
					sourceStage.DisplayOrder,
					sourceStage.ColorHex,
					sourceStage.SlaTargetHours,
					sourceStage.OwnerResponsibility,
					sourceStage.AllowedNextStagesJson)
				: AdmissionCrmPipelineStage.CreateCustom(
					idMap[sourceStage.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceStage.StageKey,
					sourceStage.BaseSystemStage,
					sourceStage.StageName,
					sourceStage.DisplayOrder,
					sourceStage.ColorHex,
					sourceStage.SlaTargetHours,
					sourceStage.OwnerResponsibility,
					sourceStage.AllowedNextStagesJson);
			stage.Update(
				sourceStage.StageName,
				sourceStage.DisplayOrder,
				sourceStage.IsEnabled,
				sourceStage.ColorHex,
				sourceStage.SlaTargetHours,
				sourceStage.OwnerResponsibility,
				sourceStage.AllowedNextStagesJson);
			await stageRepository.InsertAsync(stage, autoSave: false);
		}

		foreach (var sourceItem in source.InquirySources.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id))
		{
			var item = new AdmissionCrmInquirySource(
				idMap[sourceItem.Id],
				currentTenant.Id,
				targetAcademicYearId,
				sourceItem.Code,
				sourceItem.DisplayName,
				sourceItem.Description,
				sourceItem.GetAllowedChannels(),
				sourceItem.DisplayOrder,
				sourceItem.IsRoutingEligible);
			if (!sourceItem.IsActive)
			{
				item.SetActive(false, sourceItem.DeactivationReason ?? "Inactive in source academic year.");
			}
			await inquirySourceRepository.InsertAsync(item, autoSave: false);
		}

		foreach (var sourceItem in source.InquiryForms.OrderBy(x => x.Id))
		{
			var item = new AdmissionInquiryFormTemplate(
				idMap[sourceItem.Id],
				currentTenant.Id,
				targetAcademicYearId,
				sourceItem.TemplateName,
				sourceItem.Description,
				sourceItem.Channel,
				sourceItem.IsPublic,
				sourceItem.ConsentText,
				sourceItem.PrivacyNoticeUrl,
				RemapGuids(sourceItem.FieldsJson, idMap),
				RemapGuids(sourceItem.LocalizationsJson, idMap));
			if (!sourceItem.IsActive)
			{
				item.Deactivate();
			}
			await inquiryFormRepository.InsertAsync(item, autoSave: false);
		}

		foreach (var sourceItem in source.MessageTemplates.OrderBy(x => x.Id))
		{
			var item = new AdmissionCrmMessageTemplate(
				idMap[sourceItem.Id],
				currentTenant.Id,
				targetAcademicYearId,
				sourceItem.TemplateName,
				sourceItem.Channel,
				sourceItem.Purpose,
				sourceItem.Subject,
				RemapGuids(sourceItem.Body, idMap),
				RemapGuids(sourceItem.VariablesJson, idMap));
			item.UpdateStatus(AdmissionCrmMessageTemplateStatus.Draft, sourceItem.IsActive);
			await messageTemplateRepository.InsertAsync(item, autoSave: false);
		}

		foreach (var sourceItem in source.Playbooks.OrderBy(x => x.Id))
		{
			await playbookRepository.InsertAsync(
				new AdmissionCrmPlaybook(
					idMap[sourceItem.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceItem.Name,
					sourceItem.Version,
					sourceItem.IsFallback,
					sourceItem.RegistrationPath,
					RemapGuids(sourceItem.ApplicabilityJson, idMap),
					RemapGuids(sourceItem.StagesJson, idMap),
					RemapGuids(sourceItem.RoundsJson, idMap),
					RemapGuids(sourceItem.SeatPlansJson, idMap),
					RemapGuids(sourceItem.ExamDefinitionsJson, idMap),
					RemapGuids(sourceItem.SlotTemplatesJson, idMap),
					RemapGuids(sourceItem.AssignmentRulesJson, idMap),
					sourceItem.Notes),
				autoSave: false);
		}

		foreach (var sourceItem in source.RegistrationTemplates.OrderBy(x => x.Id))
		{
			await registrationTemplateRepository.InsertAsync(
				new AdmissionCrmRegistrationTemplate(
					idMap[sourceItem.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceItem.Name,
					sourceItem.Version,
					sourceItem.IsFallback,
					RemapGuids(sourceItem.ApplicabilityJson, idMap),
					RemapGuids(sourceItem.SectionsJson, idMap),
					sourceItem.LinkExpiryDays,
					sourceItem.AllowPartialSave,
					sourceItem.FeeRequired,
					sourceItem.FeeAmount,
					sourceItem.AutoIssueAtStage,
					sourceItem.Notes),
				autoSave: false);
		}

		foreach (var sourceItem in source.FormNumberSeries.OrderBy(x => x.Id))
		{
			await formNumberSeriesRepository.InsertAsync(
				new AdmissionCrmFormNumberSeries(
					idMap[sourceItem.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceItem.Name,
					sourceItem.Prefix,
					sourceItem.Suffix,
					sourceItem.Padding,
					nextNumber: 1,
					RemapGuids(sourceItem.ApplicabilityJson, idMap),
					sourceItem.IsActive),
				autoSave: false);
		}

		foreach (var sourceItem in source.DocumentChecklists.OrderBy(x => x.Id))
		{
			await documentChecklistRepository.InsertAsync(
				new AdmissionCrmDocumentChecklistTemplate(
					idMap[sourceItem.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceItem.Name,
					sourceItem.Version,
					sourceItem.IsFallback,
					RemapGuids(sourceItem.ApplicabilityJson, idMap),
					RemapGuids(sourceItem.RequirementsJson, idMap),
					sourceItem.ReadinessApprovalCount,
					sourceItem.Notes),
				autoSave: false);
		}

		foreach (var sourceItem in source.SeatPools.OrderBy(x => x.Id))
		{
			var targetPlaybookId = sourceItem.PlaybookId.HasValue &&
			                       idMap.TryGetValue(sourceItem.PlaybookId.Value, out var mappedPlaybookId)
				? mappedPlaybookId
				: (Guid?)null;
			var item = new AdmissionCrmSeatPool(
				idMap[sourceItem.Id],
				currentTenant.Id,
				targetAcademicYearId,
				targetPlaybookId,
				sourceItem.Code,
				sourceItem.Name,
				sourceItem.RoundCode,
				sourceItem.Campus,
				sourceItem.DesiredClass,
				sourceItem.Board,
				sourceItem.Medium,
				sourceItem.ApplicantCategory,
				sourceItem.OperationalTarget,
				sourceItem.ApprovedCapacity);
			if (sourceItem.Status != AdmissionCrmSeatPoolStatus.Active)
			{
				item.UpdateDefinition(
					targetPlaybookId,
					sourceItem.Name,
					sourceItem.RoundCode,
					sourceItem.Campus,
					sourceItem.DesiredClass,
					sourceItem.Board,
					sourceItem.Medium,
					sourceItem.ApplicantCategory,
					sourceItem.OperationalTarget,
					sourceItem.ApprovedCapacity,
					isActive: false);
			}
			await seatPoolRepository.InsertAsync(item, autoSave: false);
		}

		foreach (var sourceItem in source.FeePolicies.OrderBy(x => x.Id))
		{
			await feePolicyRepository.InsertAsync(
				new AdmissionCrmFeePolicy(
					idMap[sourceItem.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceItem.Code,
					sourceItem.Name,
					sourceItem.Version,
					RemapGuids(sourceItem.ApplicabilityJson, idMap),
					RemapGuids(sourceItem.ComponentsJson, idMap),
					sourceItem.Currency,
					sourceItem.GateType,
					sourceItem.GateMilestone,
					sourceItem.RequiredAmount,
					sourceItem.RequiredPercentage,
					sourceItem.GracePeriodHours,
					Shift(sourceItem.EffectiveFrom, dateOffset),
					Shift(sourceItem.EffectiveUntil, dateOffset)),
				autoSave: false);
		}

		foreach (var sourceItem in source.RouteDefinitions.OrderBy(x => x.Id))
		{
			await routeDefinitionRepository.InsertAsync(
				new AdmissionCrmRouteDefinition(
					idMap[sourceItem.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceItem.Code,
					sourceItem.Name,
					sourceItem.RouteType,
					sourceItem.Version,
					RemapGuids(sourceItem.ConfigurationJson, idMap),
					Shift(sourceItem.EffectiveFrom, dateOffset),
					Shift(sourceItem.EffectiveUntil, dateOffset)),
				autoSave: false);
		}

		var actorId = currentUser.Id ??
			throw new InvalidOperationException("An authenticated user is required to confirm a configuration clone.");
		foreach (var sourceItem in source.PolicyAdoptions.OrderBy(x => x.Id))
		{
			var item = new AdmissionCrmPolicyAdoption(
				idMap[sourceItem.Id],
				currentTenant.Id,
				targetAcademicYearId,
				sourceItem.PolicyPackId,
				sourceItem.PolicyPackVersion,
				RemapGuids(sourceItem.ApplicabilityJson, idMap),
				RemapGuids(sourceItem.OverridesJson, idMap),
				RemapGuids(sourceItem.ImpactPreviewJson, idMap),
				sourceItem.OverrideReason,
				Shift(sourceItem.EffectiveFrom, dateOffset),
				Shift(sourceItem.EffectiveUntil, dateOffset),
				actorId,
				clock.Now);
			if (!sourceItem.IsActive)
			{
				item.Deactivate();
			}
			await policyAdoptionRepository.InsertAsync(item, autoSave: false);
		}

		foreach (var sourceItem in source.NumberSeries.OrderBy(x => x.Id))
		{
			await numberSeriesRepository.InsertAsync(
				new AdmissionCrmNumberSeries(
					idMap[sourceItem.Id],
					currentTenant.Id,
					targetAcademicYearId,
					sourceItem.Code,
					sourceItem.Name,
					sourceItem.IdentifierType,
					MapOptional(sourceItem.EducationBoardId, idMap),
					MapOptional(sourceItem.AcademicMediumId, idMap),
					MapOptional(sourceItem.StandardId, idMap),
					sourceItem.Prefix,
					sourceItem.Suffix,
					sourceItem.Padding,
					sourceItem.StartingSequence,
					sourceItem.MaximumSequence,
					Shift(sourceItem.EffectiveFrom, dateOffset),
					Shift(sourceItem.EffectiveUntil, dateOffset)),
				autoSave: false);
		}
	}

	internal static bool IsHierarchyIndependent(AdmissionCrmNumberSeries numberSeries) =>
		!numberSeries.EducationBoardId.HasValue &&
		!numberSeries.AcademicMediumId.HasValue &&
		!numberSeries.StandardId.HasValue;

	private AdmissionCrmCycleConfiguration CloneCycle(
		AdmissionCrmCycleConfiguration source,
		Guid targetAcademicYearId,
		IReadOnlyDictionary<Guid, Guid> idMap)
	{
		var target = new AdmissionCrmCycleConfiguration(
			idMap[source.Id],
			currentTenant.Id,
			targetAcademicYearId,
			source.CycleName);
		target.Update(
			source.CycleName,
			source.IsActive,
			source.RegistrationFormRequiredBeforeExam,
			source.EntranceExamRequired,
			source.StaleAfterDays,
			source.ArchiveAfterDays,
			source.DefaultLeadSlaHours,
			source.ActivationCancellationWindowDays,
			source.DefaultExamPassingPercentage,
			source.MeritRankingMode,
			source.ExamReadinessFields,
			RemapGuids(source.LeadScoringRulesJson, idMap),
			RemapGuids(source.LeadRoutingRulesJson, idMap),
			RemapGuids(source.JourneyRulesJson, idMap),
			RemapGuids(source.ApplicantChecklistJson, idMap),
			RemapGuids(source.ChannelSettingsJson, idMap),
			RemapGuids(source.SlaPolicyJson, idMap),
			source.Notes);
		return target;
	}

	private Dictionary<Guid, Guid> BuildIdMap(
		AdmissionCrmConfigurationSnapshot source,
		IReadOnlyDictionary<Guid, Guid> hierarchyIdMap)
	{
		var ids = new List<Guid>();
		if (source.Cycle != null)
		{
			ids.Add(source.Cycle.Id);
		}
		ids.AddRange(source.PipelineStages.Select(x => x.Id));
		ids.AddRange(source.InquirySources.Select(x => x.Id));
		ids.AddRange(source.InquiryForms.Select(x => x.Id));
		ids.AddRange(source.MessageTemplates.Select(x => x.Id));
		ids.AddRange(source.Playbooks.Select(x => x.Id));
		ids.AddRange(source.RegistrationTemplates.Select(x => x.Id));
		ids.AddRange(source.FormNumberSeries.Select(x => x.Id));
		ids.AddRange(source.DocumentChecklists.Select(x => x.Id));
		ids.AddRange(source.SeatPools.Select(x => x.Id));
		ids.AddRange(source.FeePolicies.Select(x => x.Id));
		ids.AddRange(source.RouteDefinitions.Select(x => x.Id));
		ids.AddRange(source.PolicyAdoptions.Select(x => x.Id));
		ids.AddRange(source.NumberSeries.Select(x => x.Id));
		var idMap = hierarchyIdMap.ToDictionary(x => x.Key, x => x.Value);
		foreach (var id in ids.Distinct())
		{
			idMap.Add(id, guidGenerator.Create());
		}
		return idMap;
	}

	private static Guid? MapOptional(Guid? sourceId, IReadOnlyDictionary<Guid, Guid> idMap)
	{
		if (!sourceId.HasValue)
		{
			return null;
		}
		if (!idMap.TryGetValue(sourceId.Value, out var targetId))
		{
			throw new InvalidOperationException(
				$"Academic hierarchy identifier '{sourceId.Value:D}' was not mapped before configuration cloning.");
		}
		return targetId;
	}

	private static string RemapGuids(string value, IReadOnlyDictionary<Guid, Guid> idMap)
	{
		var result = value;
		foreach (var mapping in idMap)
		{
			result = result.Replace(
				mapping.Key.ToString("D"),
				mapping.Value.ToString("D"),
				StringComparison.OrdinalIgnoreCase);
		}
		return result;
	}

	private static DateTime Shift(DateTime value, TimeSpan offset) => value.Add(offset);
	private static DateTime? Shift(DateTime? value, TimeSpan offset) => value?.Add(offset);
}
