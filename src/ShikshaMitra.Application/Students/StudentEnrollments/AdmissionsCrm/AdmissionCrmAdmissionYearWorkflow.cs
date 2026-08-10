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
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionYearWorkflow(
	IRepository<AdmissionCrmAdmissionYearSetup, Guid> repository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmAdmissionYearReferenceReader referenceReader,
	AdmissionCrmAdmissionYearValidator validator,
	IAbpDistributedLock distributedLock) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<IReadOnlyList<AdmissionCrmAdmissionYearSetupDto>> GetAdmissionYearVersionsAsync(
		Guid academicYearId)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetAdmissionYearVersionsAsync));
		await referenceReader.GetAcademicYearAsync(academicYearId);
		var query = (await repository.GetQueryableAsync())
			.Where(setup =>
				setup.TenantId == CurrentTenant.Id &&
				setup.AcademicYearId == academicYearId)
			.OrderByDescending(setup => setup.Version);
		return (await AsyncExecuter.ToListAsync(query))
			.Select(AdmissionCrmAdmissionYearMapper.ToDto)
			.ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>>
		GetAdmissionYearStandardOptionsAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetAdmissionYearStandardOptionsAsync));
		await referenceReader.GetAcademicYearAsync(academicYearId);
		var rows = await referenceReader.GetStandardRowsAsync(academicYearId);
		return rows
			.OrderBy(row => row.BoardType)
			.ThenBy(row => row.MediumName)
			.ThenBy(row => row.MediumStageName)
			.ThenBy(row => row.StandardName)
			.Select(row => new AdmissionCrmAdmissionYearStandardOptionDto
			{
				StandardId = row.StandardId,
				StandardName = row.StandardName,
				MediumStageId = row.MediumStageId,
				MediumStageName = row.MediumStageName,
				MediumId = row.MediumId,
				MediumName = row.MediumName,
				BoardId = row.BoardId,
				BoardName = row.BoardType.ToString()
			})
			.ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmAdmissionYearSetupDto> CreateAdmissionYearDraftAsync(
		Guid academicYearId)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(CreateAdmissionYearDraftAsync));
		await using var lockHandle = await distributedLock.TryAcquireAsync(
			AdmissionCrmAdmissionYearMapper.GetLockKey(CurrentTenant.Id, academicYearId),
			TimeSpan.FromSeconds(15));
		if (lockHandle is null)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupBusy")
				.WithData("AcademicYearId", academicYearId);
		}

		var year = await referenceReader.GetAcademicYearAsync(academicYearId);
		var existing = await repository.GetListAsync(setup =>
			setup.TenantId == CurrentTenant.Id &&
			setup.AcademicYearId == academicYearId);
		var currentDraft = existing.FirstOrDefault(setup =>
			setup.Status == AdmissionCrmAdmissionYearSetupStatus.Draft);
		if (currentDraft is not null)
		{
			return AdmissionCrmAdmissionYearMapper.ToDto(currentDraft);
		}

		var source = existing
			.OrderByDescending(setup => setup.Version)
			.FirstOrDefault();
		var setup = source is null
			? new AdmissionCrmAdmissionYearSetup(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				1,
				year.StartDate.Date.AddMonths(-6),
				year.StartDate.Date,
				year.StartDate.Date,
				year.StartDate.Date,
				true,
				false,
				AdmissionCrmPlacementTiming.StandardBeforeActivation,
				null,
				"[]",
				JsonSerializer.Serialize(new[] { AdmissionCrmRouteType.Fresh }, AdmissionCrmSetupFormat.JsonOptions),
				"{}")
			: new AdmissionCrmAdmissionYearSetup(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				source.Version + 1,
				source.InquiryOpenDate,
				source.InquiryCloseDate,
				source.RegistrationCloseDate,
				source.ActivationTargetDate,
				source.EntranceExamRequired,
				source.FinanceVerificationRequired,
				source.PlacementTiming,
				source.DefaultOwnerUserId,
				source.TargetStandardIdsJson,
				source.RouteTypesJson,
				source.StepOwnersJson);

		await repository.InsertAsync(setup, autoSave: true);
		return AdmissionCrmAdmissionYearMapper.ToDto(setup);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmAdmissionYearSetupDto> UpdateAdmissionYearDraftAsync(
		Guid id,
		UpdateAdmissionCrmAdmissionYearSetupDto input)
	{
		var setup = await GetForCurrentTenantAsync(
			id,
			nameof(UpdateAdmissionYearDraftAsync));
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			setup.AcademicYearId,
			nameof(UpdateAdmissionYearDraftAsync));
		await using var lockHandle = await distributedLock.TryAcquireAsync(
			AdmissionCrmAdmissionYearMapper.GetLockKey(CurrentTenant.Id, setup.AcademicYearId),
			TimeSpan.FromSeconds(15));
		if (lockHandle is null)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupBusy")
				.WithData("AcademicYearId", setup.AcademicYearId);
		}

		AdmissionCrmAdmissionYearMapper.EnsureConcurrency(setup, input.ConcurrencyStamp);

		await validator.ValidateAsync(setup.AcademicYearId, input);
		setup.Update(
			input.InquiryOpenDate,
			input.InquiryCloseDate,
			input.RegistrationCloseDate,
			input.ActivationTargetDate,
			input.EntranceExamRequired,
			input.FinanceVerificationRequired,
			input.PlacementTiming,
			input.DefaultOwnerUserId,
			JsonSerializer.Serialize(input.TargetStandardIds.Distinct(), AdmissionCrmSetupFormat.JsonOptions),
			JsonSerializer.Serialize(input.RouteTypes.Distinct(), AdmissionCrmSetupFormat.JsonOptions),
			JsonSerializer.Serialize(input.StepOwners, AdmissionCrmSetupFormat.JsonOptions));
		await repository.UpdateAsync(setup, autoSave: true);
		return AdmissionCrmAdmissionYearMapper.ToDto(setup);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmAdmissionYearSetupDto> PublishAdmissionYearAsync(
		Guid id,
		PublishAdmissionCrmAdmissionYearSetupDto input)
	{
		var setup = await GetForCurrentTenantAsync(id, nameof(PublishAdmissionYearAsync));
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			setup.AcademicYearId,
			nameof(PublishAdmissionYearAsync));
		await using var lockHandle = await distributedLock.TryAcquireAsync(
			AdmissionCrmAdmissionYearMapper.GetLockKey(CurrentTenant.Id, setup.AcademicYearId),
			TimeSpan.FromSeconds(15));
		if (lockHandle is null)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupBusy")
				.WithData("AcademicYearId", setup.AcademicYearId);
		}

		AdmissionCrmAdmissionYearMapper.EnsureConcurrency(setup, input.ConcurrencyStamp);
		var candidate = AdmissionCrmAdmissionYearMapper.ToDto(setup);
		await validator.ValidateAsync(
			setup.AcademicYearId,
			new UpdateAdmissionCrmAdmissionYearSetupDto
			{
				ConcurrencyStamp = candidate.ConcurrencyStamp,
				InquiryOpenDate = candidate.InquiryOpenDate,
				InquiryCloseDate = candidate.InquiryCloseDate,
				RegistrationCloseDate = candidate.RegistrationCloseDate,
				ActivationTargetDate = candidate.ActivationTargetDate,
				EntranceExamRequired = candidate.EntranceExamRequired,
				FinanceVerificationRequired = candidate.FinanceVerificationRequired,
				PlacementTiming = candidate.PlacementTiming,
				DefaultOwnerUserId = candidate.DefaultOwnerUserId,
				TargetStandardIds = candidate.TargetStandardIds,
				RouteTypes = candidate.RouteTypes,
				StepOwners = candidate.StepOwners
			},
			requirePublishReady: true);

		var published = await repository.FirstOrDefaultAsync(existing =>
			existing.TenantId == CurrentTenant.Id &&
			existing.AcademicYearId == setup.AcademicYearId &&
			existing.Status == AdmissionCrmAdmissionYearSetupStatus.Published);
		if (published is not null && published.Id != setup.Id)
		{
			published.Supersede();
			await repository.UpdateAsync(published, autoSave: true);
		}

		var snapshot = JsonSerializer.Serialize(candidate, AdmissionCrmSetupFormat.JsonOptions);
		setup.Publish(snapshot, CurrentUser.GetId(), Clock.Now);
		await repository.UpdateAsync(setup, autoSave: true);
		return AdmissionCrmAdmissionYearMapper.ToDto(setup);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmAdmissionYearSetupDto> CloseAdmissionYearAsync(
		Guid id,
		CloseAdmissionCrmAdmissionYearSetupDto input)
	{
		var setup = await GetForCurrentTenantAsync(id, nameof(CloseAdmissionYearAsync));
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			setup.AcademicYearId,
			nameof(CloseAdmissionYearAsync));
		await using var lockHandle = await distributedLock.TryAcquireAsync(
			AdmissionCrmAdmissionYearMapper.GetLockKey(CurrentTenant.Id, setup.AcademicYearId),
			TimeSpan.FromSeconds(15));
		if (lockHandle is null)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupBusy")
				.WithData("AcademicYearId", setup.AcademicYearId);
		}

		AdmissionCrmAdmissionYearMapper.EnsureConcurrency(setup, input.ConcurrencyStamp);
		setup.Close(input.Reason, CurrentUser.GetId(), Clock.Now);
		await repository.UpdateAsync(setup, autoSave: true);
		return AdmissionCrmAdmissionYearMapper.ToDto(setup);
	}

	private async Task<AdmissionCrmAdmissionYearSetup> GetForCurrentTenantAsync(Guid id, string operation)
	{
		var setup = await repository.FindAsync(id);
		if (setup is null || setup.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupNotFound")
				.WithData("SetupId", id).WithData("Operation", operation);
		}
		return setup;
	}
}