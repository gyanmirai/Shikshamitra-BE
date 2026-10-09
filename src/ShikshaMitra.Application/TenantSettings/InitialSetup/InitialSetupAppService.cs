using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Identity;

namespace ShikshaMitra.TenantSettings.InitialSetup;

[Authorize, RemoteService(false), UnitOfWork(isTransactional: true)]
public partial class InitialSetupAppService(
	InitialSetupStatusReader reader,
	IRepository<InstituteSetupProgress, Guid> progressRepository,
	InitialSetupProfileWriter profileWriter,
	InitialSetupAcademicWriter academicWriter,
	InitialSetupReadiness readiness, IIdentityUserAppService identityUsers) : ShikshaMitraAppService, IInitialSetupAppService
{
	public async Task<InitialSetupDto> GetAsync() => await reader.MapAsync(await reader.ReadAsync());
	public async Task<InitialSetupDto> SaveDraftAsync(SaveInitialSetupDraftInput input)
	{
		var progress = await EditableAsync(input);
		Validate(input.Draft); Validate(input.Draft.Profile);
		progress.SaveDraft(input.CurrentStep, JsonSerializer.Serialize(input.Draft));
		return await SaveAsync(progress);
	}
	public async Task<InitialSetupDto> AdvanceAsync(AdvanceSetupInput input)
	{
		var progress = await EditableAsync(input);
		if (input.Step != 0 && input.Step != 4) throw new BusinessException(InitialSetupErrorCodes.SaveStepRequired);
		progress.Advance(input.Step);
		return await SaveAsync(progress);
	}
	public async Task<InitialSetupDto> CompleteAsync(SetupConcurrencyInput input)
	{
		var progress = await reader.ReadAsync();
		if (progress.CompletedAt.HasValue) return await reader.MapAsync(progress);
		progress = await EditableAsync(input);
		await readiness.CheckAsync(progress);
		progress.Complete(Clock.Now, CurrentUser.Id!.Value);
		return await SaveAsync(progress);
	}
	private async Task<InstituteSetupProgress> EditableAsync(object input)
	{
		reader.RequireTenant();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.BasicInformation.Edit);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Add);
		Validate(input);
		var progress = await reader.ReadAsync();
		var stamp = input is SetupConcurrencyInput common ? common.ConcurrencyStamp : ((SaveSetupProfileInput)input).ConcurrencyStamp;
		if (progress.ConcurrencyStamp != stamp) throw new AbpDbConcurrencyException();
		if (progress.CompletedAt.HasValue) throw new BusinessException(InitialSetupErrorCodes.AlreadyCompleted);
		if (!await progressRepository.AnyAsync(x => x.Id == progress.Id))
			await progressRepository.InsertAsync(progress, autoSave: true);
		return progress;
	}
	private async Task<InitialSetupDto> SaveAsync(InstituteSetupProgress progress)
	{
		await progressRepository.UpdateAsync(progress);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.MapAsync(progress);
	}
	private static void Validate(object input) => Validator.ValidateObject(input, new ValidationContext(input), true);
}
