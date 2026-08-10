using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicationStagePolicyWorkflow(
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmCycleConfigurationWorkflow configurationWorkflow,
	AdmissionCrmApplicantCountReader applicantCountReader) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmApplicationStagePolicyDto> GetApplicationStagePolicyAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetApplicationStagePolicyAsync));
		var configuration = await GetConfigurationAsync(academicYearId);
		var applicantCounts = await applicantCountReader.GetAsync([academicYearId]);
		return AdmissionCrmApplicationStagePolicyMapper.Build(configuration, applicantCounts.GetValueOrDefault(academicYearId));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmApplicationStagePolicyImpactDto> PreviewApplicationStagePolicyAsync(
		Guid academicYearId,
		UpdateAdmissionCrmApplicationStagePolicyDto input)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(PreviewApplicationStagePolicyAsync));
		var configuration = await GetConfigurationAsync(academicYearId);
		var applicantCounts = await applicantCountReader.GetAsync([academicYearId]);
		var applicantCount = applicantCounts.GetValueOrDefault(academicYearId);
		var candidate = AdmissionCrmApplicationStagePolicyNormalizer.Normalize(configuration, input);
		return AdmissionCrmApplicationStagePolicyMapper.BuildImpact(configuration, candidate, applicantCount);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmApplicationStagePolicyDto> UpdateApplicationStagePolicyAsync(
		Guid academicYearId,
		UpdateAdmissionCrmApplicationStagePolicyDto input)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(UpdateApplicationStagePolicyAsync));
		var configuration = await GetConfigurationAsync(academicYearId);
		if (configuration.JourneyLocked)
		{
			throw new BusinessException("AdmissionCrm:JourneyLocked");
		}

		var applicantCounts = await applicantCountReader.GetAsync([academicYearId]);
		var applicantCount = applicantCounts.GetValueOrDefault(academicYearId);
		var candidate = AdmissionCrmApplicationStagePolicyNormalizer.Normalize(configuration, input);
		var impact = AdmissionCrmApplicationStagePolicyMapper.BuildImpact(configuration, candidate, applicantCount);
		if (impact.RequiresConfirmation &&
			!string.Equals(input.ExpectedImpactFingerprint, impact.ImpactFingerprint, StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:StagePolicyImpactPreviewRequired")
				.WithData("ExpectedImpactFingerprint", impact.ImpactFingerprint);
		}

		configuration.UpdateApplicationStagePolicy(AdmissionCrmApplicationStagePolicyMapper.Serialize(candidate));
		await cycleRepository.UpdateAsync(configuration, autoSave: true);
		return AdmissionCrmApplicationStagePolicyMapper.Build(configuration, applicantCount);
	}

	private async Task<AdmissionCrmCycleConfiguration> GetConfigurationAsync(Guid academicYearId)
	{
		var configuration = await configurationWorkflow.GetAsync(academicYearId);
		return await cycleRepository.GetAsync(configuration.Id);
	}
}