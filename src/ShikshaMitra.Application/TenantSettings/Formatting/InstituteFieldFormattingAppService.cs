using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.Formatting;

[RemoteService(false)]
[Authorize]
public class InstituteFieldFormattingAppService(InstituteFieldFormattingStore store) : ApplicationService, IInstituteFieldFormattingAppService
{
	public async Task<InstituteFieldFormattingDto> GetAsync(string scope)
	{
		await CheckAsync(scope, false);
		return new() { Fields = await store.GetAsync(scope) };
	}

	[UnitOfWork(true)]
	public async Task<InstituteFieldFormattingDto> UpdateAsync(string scope, InstituteFieldFormattingDto input)
	{
		await CheckAsync(scope, true);
		Check.NotNull(input, nameof(input));
		await store.SetAsync(scope, input.Fields);
		return new() { Fields = input.Fields };
	}

	[UnitOfWork(false)]
	public async Task<AcademicNamePreviewDto> PreviewAsync(string scope, InstituteNamePreviewInput input)
	{
		await CheckAsync(scope, false);
		if (input?.Value == null || input.Value.Length > InstituteNamePreviewInput.MaxValueLength || !Enum.IsDefined(input.TextCase))
			throw new AbpValidationException("Enter a preview value of at most 2000 characters and choose a supported text case.");
		return new() { Value = AcademicNameFormatter.Format(input.Value, input.TextCase) };
	}

	private async Task CheckAsync(string scope, bool write)
	{
		var permission = (scope, write) switch
		{
			("buildings", false) => ShikshaMitraSubmodulePermissions.InstituteSetup.BuildingManagement.View,
			("buildings", true) => ShikshaMitraSubmodulePermissions.InstituteSetup.BuildingManagement.Add,
			("roomTypes", false) => ShikshaMitraSubmodulePermissions.InstituteSetup.RoomTypeManagement.View,
			("roomTypes", true) => ShikshaMitraSubmodulePermissions.InstituteSetup.RoomTypeManagement.Add,
			("rooms", false) => ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.View,
			("rooms", true) => ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Add,
			("shifts", false) => ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.View,
			("shifts", true) => ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.Edit,
			("schoolIdentity", false) => SchoolIdentityPermissions.View,
			("schoolIdentity", true) => SchoolIdentityPermissions.ManageDefaults,
			("schoolDocuments", false) => SchoolDocumentPermissions.View,
			("schoolDocuments", true) => SchoolDocumentPermissions.Add,
			_ => throw new AbpValidationException("Choose a supported Institute Setup form.")
		};
		await AuthorizationService.CheckAsync(permission);
		if (scope == "schoolIdentity" && write) await AuthorizationService.CheckAsync(SchoolIdentityPermissions.AccessSignature);
		if (!CurrentTenant.Id.HasValue) throw new AbpAuthorizationException();
	}
}
