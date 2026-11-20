using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.SettingManagement;

namespace ShikshaMitra.Library.Policies;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View)]
public class LibraryCalendarAppService(LibraryCalendarService calendar, ISettingManager settings,
	IRepository<LibraryCalendarClosure, Guid> closures, IRepository<LibrarySection, Guid> sections,
	ILibraryAccessService access, ILibraryInventoryLock inventoryLock) : ApplicationService, ILibraryCalendarAppService
{
	public async Task<LibraryCalendarSettingsDto> GetSettingsAsync(Guid? academicYearId = null)
	{
		var context = await access.GetContextAsync(academicYearId);
		var result = await calendar.GetSettingsAsync();
		result.CanManageCalendar = context.UserScope.HasTenantWideScope() &&
			await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add);
		if (result.NeedsConfiguration) result.ConfigurationIssues.Add("Confirm a valid Library timezone before publishing an automated policy.");
		if (!academicYearId.HasValue) result.ConfigurationIssues.Add("Select an academic year to check Library opening hours.");
		else
		{
			try
			{
				result.HasValidOpenWeekday = (await calendar.GetOpenWeekdaysAsync(academicYearId.Value)).Count > 0;
				if (result.HasValidOpenWeekday == false) result.ConfigurationIssues.Add("Configure at least one valid open Library weekday.");
			}
			catch (BusinessException error)
			{
				result.HasValidOpenWeekday = false;
				result.ConfigurationIssues.Add(error.Message);
			}
		}
		result.CanPublishAutomatedPolicy = !result.NeedsConfiguration && result.HasValidOpenWeekday == true;
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add)]
	public async Task<LibraryCalendarSettingsDto> UpdateSettingsAsync(LibraryCalendarSettingsRequest input)
	{
		await EnsureManagerAsync();
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var zone = LibraryCalendarService.ResolveZone(input.TimeZoneId.Trim());
		if (CurrentTenant.Id.HasValue) await settings.SetForTenantAsync(CurrentTenant.Id.Value, LibraryPolicySettingNames.CalendarTimeZone, zone.Id, forceToSet: true);
		else await settings.SetGlobalAsync(LibraryPolicySettingNames.CalendarTimeZone, zone.Id);
		return await GetSettingsAsync();
	}

	public async Task<PaginatedResponse<LibraryCalendarClosureDto>> GetClosuresAsync(Guid academicYearId, int page = 1, int pageSize = 20)
	{
		await access.GetContextAsync(academicYearId);
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var query = await closures.GetQueryableAsync();
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.FromDate).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize));
		return new()
		{
			TotalCount = count, CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(count / (double)pageSize),
			Items = ObjectMapper.Map<System.Collections.Generic.List<LibraryCalendarClosure>, System.Collections.Generic.List<LibraryCalendarClosureDto>>(rows)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add)]
	public async Task<LibraryCalendarClosureDto> CreateClosureAsync(Guid academicYearId, LibraryCalendarClosureRequest input)
	{
		await access.GetContextAsync(academicYearId);
		await EnsureManagerAsync();
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		await EnsureSectionAsync(input.SectionId);
		var closure = new LibraryCalendarClosure(GuidGenerator.Create(), CurrentTenant.Id, input.FromDate, input.ToDate, input.SectionId, input.Reason);
		await closures.InsertAsync(closure, true);
		return ObjectMapper.Map<LibraryCalendarClosure, LibraryCalendarClosureDto>(closure);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add)]
	public async Task<LibraryCalendarClosureDto> UpdateClosureAsync(Guid closureId, LibraryCalendarClosureRequest input)
	{
		await EnsureManagerAsync();
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var closure = await closures.GetAsync(closureId);
		await EnsureSectionAsync(input.SectionId);
		closure.Update(input.FromDate, input.ToDate, input.SectionId, input.Reason, input.ConcurrencyStamp);
		await closures.UpdateAsync(closure, true);
		return ObjectMapper.Map<LibraryCalendarClosure, LibraryCalendarClosureDto>(closure);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add)]
	public async Task DeleteClosureAsync(Guid closureId)
	{
		await EnsureManagerAsync();
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		await closures.DeleteAsync(await closures.GetAsync(closureId), true);
	}

	public async Task<LibraryCalendarCalculationDto> CalculateAsync(LibraryCalendarCalculateRequest input)
	{
		var context = await access.GetContextAsync(input.AcademicYearId);
		await EnsureSectionAsync(input.SectionId);
		var result = await calendar.CalculateAsync(input);
		result.CanManageCalendar = context.UserScope.HasTenantWideScope() &&
			await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add);
		return result;
	}

	private async Task EnsureManagerAsync()
	{
		if (!(await access.GetContextAsync()).UserScope.HasTenantWideScope())
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}
	private async Task EnsureSectionAsync(Guid? sectionId)
	{
		if (sectionId.HasValue && !await sections.AnyAsync(x => x.Id == sectionId.Value))
			throw new BusinessException(LibraryErrorCodes.SectionNotFound);
	}
}
