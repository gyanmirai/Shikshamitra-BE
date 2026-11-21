using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Catalogue;

[Authorize]
[RemoteService(false)]
public class LibraryCatalogueConfigurationAppService(IPermissionChecker permissions, ILibraryAccessService access,
	ILibraryInventoryLock inventoryLock, IRepository<LibraryCatalogueConfiguration, Guid> configurations,
	LibraryCatalogueConfigurationReader reader) : ApplicationService, ILibraryCatalogueConfigurationAppService
{
	public async Task<LibraryCatalogueConfigurationDto> GetAsync()
	{
		var context = await RequireAsync(update: false);
		var result = await reader.ReadAsync();
		result.CanManageConfiguration = context.UserScope.HasTenantWideScope() &&
			await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update);
		return result;
	}

	public async Task<LibraryCatalogueConfigurationDto> UpdateAsync(LibraryCatalogueConfigurationRequest input)
	{
		await RequireAsync(update: true);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var saved = await configurations.FindAsync(row => row.TenantId == CurrentTenant.Id);
		if (saved == null)
		{
			if (!string.Equals(input.ExpectedRevision, LibraryCatalogueRules.DefaultsRevision, StringComparison.Ordinal))
				throw new BusinessException(LibraryErrorCodes.CatalogueConfigurationChanged, "The catalogue configuration changed. Reload it before saving.");
			await configurations.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id, input.Definition), true);
		}
		else
		{
			saved.Revise(input.Definition, input.ExpectedRevision);
			await configurations.UpdateAsync(saved, true);
		}
		return await GetAsync();
	}

	private async Task<LibraryAcademicContext> RequireAsync(bool update)
	{
		if (!CurrentUser.IsAuthenticated || !CurrentTenant.Id.HasValue ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View) ||
			update && !await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update))
			throw new AbpAuthorizationException();
		var context = await access.GetContextAsync();
		if (update && !context.UserScope.HasTenantWideScope()) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return context;
	}
}
