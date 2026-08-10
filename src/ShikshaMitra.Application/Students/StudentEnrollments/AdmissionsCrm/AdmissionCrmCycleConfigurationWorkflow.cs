using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCycleConfigurationWorkflow(
	IRepository<AdmissionCrmCycleConfiguration, Guid> repository,
	AdmissionCrmConfigurationAccess access,
	AdmissionCrmStaffEligibilityService staffEligibility,
	AdmissionCrmConfigurationImpactWorkflow impact) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmCycleConfigurationDto> GetSchoolDefaultAsync()
	{
		await EnsureManageAsync();
		return AdmissionCrmConfigurationMapper.Cycle(await access.EnsureSchoolDefaultCycleAsync());
	}

	public async Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultAsync(UpdateAdmissionCrmCycleConfigurationDto input)
	{
		await EnsureManageAsync();
		await access.EnsureTenantMutationAsync("UpdateSchoolDefaultCycleConfigurationAsync");
		AdmissionCrmConfigurationMapper.ValidateSlaPolicy(input.SlaPolicy);
		await ValidateStaffAsync(input);
		var configuration = await access.EnsureSchoolDefaultCycleAsync();
		Apply(configuration, input);
		await repository.UpdateAsync(configuration, autoSave: true);
		return AdmissionCrmConfigurationMapper.Cycle(configuration);
	}

	public async Task<AdmissionCrmCycleConfigurationDto> GetAsync(Guid yearId)
	{
		await EnsureManageAsync();
		return AdmissionCrmConfigurationMapper.Cycle(await access.EnsureCycleAsync(yearId));
	}

	public async Task<AdmissionCrmCycleConfigurationDto> UpdateAsync(Guid yearId, UpdateAdmissionCrmCycleConfigurationDto input)
	{
		await EnsureManageAsync();
		AdmissionCrmConfigurationMapper.ValidateSlaPolicy(input.SlaPolicy);
		await access.EnsureYearMutationAsync(yearId, "UpdateCycleConfigurationAsync");
		await impact.EnsureFingerprintAsync(yearId, new PreviewAdmissionCrmConfigurationImpactDto
			{ ProposedCycleConfiguration = input }, input.ImpactPreviewFingerprint, "UpdateCycleConfigurationAsync");
		var configuration = await access.EnsureCycleAsync(yearId);
		EnsureJourneyEditable(configuration);
		await ValidateStaffAsync(input);
		Apply(configuration, input);
		await repository.UpdateAsync(configuration, autoSave: true);
		return AdmissionCrmConfigurationMapper.Cycle(configuration);
	}

	public async Task<AdmissionCrmCycleConfigurationDto> LockAsync(Guid yearId, AdmissionCrmJourneyLockDto input)
	{
		await EnsureManageAsync();
		await access.EnsureYearMutationAsync(yearId, "LockJourneyAsync");
		var configuration = await access.EnsureCycleAsync(yearId);
		configuration.LockJourney(CurrentUser.Id, Clock.Now, input.Reason);
		await repository.UpdateAsync(configuration, autoSave: true);
		return AdmissionCrmConfigurationMapper.Cycle(configuration);
	}

	public async Task<AdmissionCrmCycleConfigurationDto> UnlockAsync(Guid yearId, AdmissionCrmJourneyLockDto input)
	{
		await EnsureManageAsync();
		if (string.IsNullOrWhiteSpace(input.Reason)) throw new BusinessException("AdmissionCrm:JourneyUnlockReasonRequired");
		await access.EnsureYearMutationAsync(yearId, "UnlockJourneyAsync");
		var configuration = await access.EnsureCycleAsync(yearId);
		configuration.UnlockJourney();
		await repository.UpdateAsync(configuration, autoSave: true);
		return AdmissionCrmConfigurationMapper.Cycle(configuration);
	}

	public static void EnsureJourneyEditable(AdmissionCrmCycleConfiguration configuration)
	{
		if (configuration.JourneyLocked) throw new BusinessException("AdmissionCrm:JourneyLocked");
	}

	private async Task ValidateStaffAsync(UpdateAdmissionCrmCycleConfigurationDto input) =>
		await staffEligibility.ValidateConfigurationStaffAsync(input.LeadRoutingSettings, input.ChannelSettings,
			input.JourneyRules, input.SlaPolicy);

	private static void Apply(AdmissionCrmCycleConfiguration configuration, UpdateAdmissionCrmCycleConfigurationDto input) =>
		configuration.Update(input.CycleName, input.IsActive, input.RegistrationFormRequiredBeforeExam,
			input.EntranceExamRequired, input.StaleAfterDays, input.ArchiveAfterDays, input.DefaultLeadSlaHours,
			input.ActivationCancellationWindowDays, input.DefaultExamPassingPercentage, input.MeritRankingMode,
			input.ExamReadinessFields, AdmissionCrmLeadScoreCalculator.SerializeRules(input.LeadScoringRules),
			AdmissionCrmLeadRoutingRuleMatcher.SerializeSettings(input.LeadRoutingSettings),
			AdmissionCrmJourneyRuleSerializer.SerializeRules(input.JourneyRules),
			AdmissionCrmApplicantChecklistSerializer.SerializeItems(input.ApplicantChecklist),
			AdmissionCrmChannelSettingsSerializer.SerializeSettings(input.ChannelSettings),
			JsonSerializer.Serialize(input.SlaPolicy), input.Notes);

	private Task EnsureManageAsync() => AuthorizationService.CheckAsync(
		ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration);
}
