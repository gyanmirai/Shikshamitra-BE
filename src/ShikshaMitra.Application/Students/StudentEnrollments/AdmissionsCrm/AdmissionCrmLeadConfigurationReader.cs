using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadConfigurationReader(
	IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	AdmissionCrmLeadInquirySourceReader inquirySources) : ApplicationService, ITransientDependency
{
	internal Task<string?> GetInquirySourceSnapshotAsync(Guid? inquirySourceId) =>
		inquirySources.GetInquirySourceSnapshotAsync(inquirySourceId);

	internal async Task<AdmissionCrmCycleConfiguration> GetCycleConfigurationOrDefaultAsync(Guid academicYearId)
	{
		var configuration = await cycleRepository.FirstOrDefaultAsync(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		return configuration ?? new AdmissionCrmCycleConfiguration(
			GuidGenerator.Create(), CurrentTenant.Id, academicYearId, "Default Admission Year");
	}

	internal async Task EnsureConfiguredStageTransitionAllowedAsync(
		AdmissionInquiryLead lead,
		AdmissionInquiryStage requestedStage)
	{
		if (lead.Stage == requestedStage)
		{
			return;
		}

		var queryable = await stageRepository.GetQueryableAsync();
		var currentStageConfiguration = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(stage =>
			stage.TenantId == CurrentTenant.Id &&
			stage.AcademicYearId == lead.AcademicYearId &&
			stage.SystemStage == lead.Stage));
		if (string.IsNullOrWhiteSpace(currentStageConfiguration?.AllowedNextStagesJson))
		{
			return;
		}

		var allowedNextStages = DeserializeAllowedNextStages(
			currentStageConfiguration.AllowedNextStagesJson);
		if (allowedNextStages.Contains(requestedStage))
		{
			return;
		}

		throw new BusinessException("AdmissionCrm:StageTransitionNotAllowed")
			.WithData("LeadId", lead.Id)
			.WithData("CurrentStage", lead.Stage)
			.WithData("RequestedStage", requestedStage);
	}

	internal static IReadOnlyList<AdmissionInquiryStage> DeserializeAllowedNextStages(
		string allowedNextStagesJson)
	{
		try
		{
			return JsonSerializer.Deserialize<List<AdmissionInquiryStage>>(allowedNextStagesJson)
				   ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	internal async Task<Dictionary<AdmissionInquiryStage, int>> GetStageSlaThresholdsAsync(Guid academicYearId)
	{
		var queryable = await stageRepository.GetQueryableAsync();
		var stages = await AsyncExecuter.ToListAsync(queryable.Where(stage =>
			stage.TenantId == CurrentTenant.Id &&
			stage.AcademicYearId == academicYearId &&
			stage.SlaTargetHours.HasValue));
		return stages.ToDictionary(
			stage => stage.SystemStage,
			stage => stage.SlaTargetHours!.Value);
	}

	internal static int GetSlaThresholdHours(
		AdmissionInquiryStage stage,
		IReadOnlyDictionary<AdmissionInquiryStage, int> stageSlaThresholds,
		int defaultLeadSlaHours)
	{
		return stageSlaThresholds.TryGetValue(stage, out var thresholdHours)
			? thresholdHours
			: defaultLeadSlaHours;
	}

	internal async Task<AdmissionCrmLeadScoringRulesDto> GetLeadScoringRulesAsync(Guid academicYearId)
	{
		var configuration = await GetCycleConfigurationOrDefaultAsync(academicYearId);
		return AdmissionCrmLeadScoreCalculator.DeserializeRules(configuration.LeadScoringRulesJson);
	}
}