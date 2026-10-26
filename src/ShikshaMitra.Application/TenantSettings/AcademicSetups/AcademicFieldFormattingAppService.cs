using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

[RemoteService(false)]
[Authorize]
public class AcademicFieldFormattingAppService(AcademicFieldFormattingStore store) : ApplicationService, IAcademicFieldFormattingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View)]
	public async Task<AcademicFieldFormattingDto> GetAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View);
		EnsureTenant();
		return Map(await store.GetAsync());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View)]
	[UnitOfWork(false)]
	public async Task<AcademicNamePreviewDto> PreviewAsync(AcademicNamePreviewInput input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View);
		EnsureTenant();
		var value = input?.Value;
		if (input == null || value == null || value.Length > AcademicNamePreviewInput.MaxValueLength || !Enum.IsDefined(input.TextCase))
			throw new AbpValidationException("Enter a preview value of at most 512 characters and choose a supported text case.");
		return new() { Value = AcademicNameFormatter.Format(value, input.TextCase) };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit)]
	[UnitOfWork(true)]
	public async Task<AcademicFieldFormattingDto> UpdateAsync(AcademicFieldFormattingDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
		EnsureTenant();
		Check.NotNull(input, nameof(input));
		var rules = new AcademicFieldFormattingRules
		{
			Board = input.Board, Medium = input.Medium, Stage = input.Stage,
			Standard = input.Standard, Division = input.Division
		};
		await store.SetAsync(rules);
		return Map(rules);
	}

	private void EnsureTenant()
	{
		if (!CurrentTenant.Id.HasValue) throw new AbpAuthorizationException();
	}

	private static AcademicFieldFormattingDto Map(AcademicFieldFormattingRules rules) => new()
	{
		Board = rules.Board, Medium = rules.Medium, Stage = rules.Stage,
		Standard = rules.Standard, Division = rules.Division
	};
}
