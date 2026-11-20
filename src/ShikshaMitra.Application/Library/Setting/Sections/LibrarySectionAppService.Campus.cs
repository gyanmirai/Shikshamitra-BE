using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Setting.Sections.Dtos;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement;

namespace ShikshaMitra.Library.Setting.Sections;

public partial class LibrarySectionAppService
{
	[Authorize(Actions.Add)]
	public async Task<List<LibrarySectionCampusDto>> GetCampusOptionsAsync(Guid academicYearId)
	{
		var tenant = await RequireCampusAccessAsync(academicYearId);
		return await AsyncExecuter.ToListAsync((await campuses.GetQueryableAsync())
			.Where(row => row.TenantId == tenant && row.IsActive)
			.OrderBy(row => row.Name).ThenBy(row => row.Code).ThenBy(row => row.Id)
			.Select(row => new LibrarySectionCampusDto { Id = row.Id, Name = row.Name, Code = row.Code }));
	}

	[Authorize(Actions.Add)]
	[UnitOfWork(true)]
	public async Task<SectionDetailsResponse> BindCampusAsync(Guid sectionId, Guid academicYearId, Guid campusId)
	{
		var tenant = await RequireCampusAccessAsync(academicYearId);
		await inventoryLock.AcquireAsync(tenant);
		var section = await _sectionRepository.FindAsync(row => row.TenantId == tenant && row.Id == sectionId)
			?? throw new BusinessException(LibraryErrorCodes.SectionNotFound);
		if (section.CampusId == campusId) return await MapDetailsAsync(section);
		if (!await AsyncExecuter.AnyAsync((await campuses.GetQueryableAsync())
			.Where(row => row.TenantId == tenant && row.Id == campusId && row.IsActive)))
			throw new BusinessException(LibraryErrorCodes.SectionCampusUnavailable);
		section.BindCampus(campusId);
		await _sectionRepository.UpdateAsync(section, autoSave: true);
		return await MapDetailsAsync(section);
	}

	private async Task<Guid> RequireCampusAccessAsync(Guid year)
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id is not Guid actor || actor == Guid.Empty ||
			CurrentTenant.Id is not Guid tenant || tenant == Guid.Empty || CurrentUser.TenantId != tenant ||
			!await permissions.IsGrantedAsync(Actions.View) || !await permissions.IsGrantedAsync(Actions.Add))
			throw new AbpAuthorizationException();
		try
		{
			var context = await libraryAccess.GetContextAsync(year);
			if (context.UserScope.HasTenantWideScope()) return tenant;
		}
		catch (BusinessException exception) when (exception.Code == UserScopeErrorCodes.AccessDenied) { }
		throw new AbpAuthorizationException();
	}

	private async Task<Dictionary<Guid, string>> GetCampusNamesAsync(IEnumerable<LibrarySection> sections)
	{
		var campusIds = sections.Where(row => row.CampusId.HasValue).Select(row => row.CampusId!.Value).Distinct().ToArray();
		if (campusIds.Length == 0 || CurrentTenant.Id is not Guid tenant) return [];
		var rows = await AsyncExecuter.ToListAsync((await campuses.GetQueryableAsync())
			.Where(row => row.TenantId == tenant && Enumerable.Contains(campusIds, row.Id)).Select(row => new { row.Id, row.Name }));
		return rows.ToDictionary(row => row.Id, row => row.Name);
	}
}
