using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Policies;

[RemoteService(false)]
[Authorize]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View)]
public class LibraryRolloverAppService(ILibraryAccessService access, IPermissionChecker permissions,
	IRepository<AcademicYear, Guid> years, IRepository<LibraryBorrowingPolicyVersion, Guid> policies,
	LibraryPolicyTargetAccess policyAccess, LibraryCalendarService calendar, LibraryStoredLoanScope storedLoans)
	: ApplicationService, ILibraryRolloverAppService
{
	public async Task<List<LibraryRolloverYearDto>> GetOptionsAsync()
	{
		var context = await RequireContextAsync();
		var query = await YearQueryAsync(context);
		return (await AsyncExecuter.ToListAsync(query.OrderByDescending(year => year.StartDate).ThenBy(year => year.Id)))
			.Select(MapYear).ToList();
	}

	public async Task<LibraryRolloverPreviewDto> PreviewAsync(LibraryRolloverInput input)
	{
		var context = await RequireContextAsync();
		if (input == null || input.SourceAcademicYearId == input.TargetAcademicYearId ||
			input.SourceAcademicYearId == Guid.Empty || input.TargetAcademicYearId == Guid.Empty)
			throw Invalid();
		if (!context.AllowedAcademicYearIds.Contains(input.SourceAcademicYearId) ||
			!context.AllowedAcademicYearIds.Contains(input.TargetAcademicYearId)) throw new AbpAuthorizationException();
		var available = await AsyncExecuter.ToListAsync((await YearQueryAsync(context)).Where(year =>
			year.Id == input.SourceAcademicYearId || year.Id == input.TargetAcademicYearId));
		var source = available.SingleOrDefault(year => year.Id == input.SourceAcademicYearId);
		var target = available.SingleOrDefault(year => year.Id == input.TargetAcademicYearId);
		if (source == null || target == null) throw new AbpAuthorizationException();
		var sourceYear = MapYear(source);
		var targetYear = MapYear(target);
		var day = input.SchoolDate ?? targetYear.StartsOn;
		if (targetYear.StartsOn <= sourceYear.StartsOn || day < targetYear.StartsOn || day > targetYear.EndsOn)
			throw Invalid();

		var policyQuery = await policyAccess.ApplyReadScopeAsync((await policies.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id), context);
		var effective = policyQuery.Where(row =>
			(row.OriginAcademicYearId == targetYear.Id || !row.EducationMediumStageId.HasValue && !row.StandardId.HasValue) &&
			(row.Status == LibraryBorrowingPolicyState.Published || row.Status == LibraryBorrowingPolicyState.Archived) &&
			row.EffectiveFrom <= day && (!row.EffectiveTo.HasValue || row.EffectiveTo >= day) &&
			(!row.ArchivedFrom.HasValue || row.ArchivedFrom > day));
		var draft = policyQuery.Where(row => row.OriginAcademicYearId == targetYear.Id && row.Status == LibraryBorrowingPolicyState.Draft);
		var schoolWide = context.UserScope.HasTenantWideScope();
		return new(sourceYear, targetYear, day, schoolWide ? "School" : "Assigned", await ReadCalendarAsync(targetYear.Id),
			await PolicySummaryAsync(effective), await PolicySummaryAsync(draft), await ReadLoansAsync(context, sourceYear.Id));
	}

	private async Task<LibraryAcademicContext> RequireContextAsync()
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id is not Guid actor || actor == Guid.Empty ||
			CurrentTenant.Id is not Guid tenant || tenant == Guid.Empty || CurrentUser.TenantId != tenant ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View))
			throw new AbpAuthorizationException();
		try { return await access.GetContextAsync(); }
		catch (BusinessException exception) when (exception.Code == UserScopeErrorCodes.AccessDenied) { throw new AbpAuthorizationException(); }
	}

	private async Task<IQueryable<AcademicYear>> YearQueryAsync(LibraryAcademicContext context)
	{
		var allowed = context.AllowedAcademicYearIds.ToArray();
		return (await years.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(allowed, row.Id));
	}

	private async Task<List<LibraryRolloverPolicyDto>> PolicySummaryAsync(IQueryable<LibraryBorrowingPolicyVersion> query) =>
		await AsyncExecuter.ToListAsync(query.OrderBy(row => row.Role).ThenBy(row => row.Name).ThenByDescending(row => row.Version).ThenBy(row => row.Id)
			.Select(row => new LibraryRolloverPolicyDto(row.Id, row.Name, row.Version, row.Role, row.Terms.DueDateMode,
				row.EffectiveFrom, row.EffectiveTo, row.OriginAcademicYearId,
				row.BookCategoryId.HasValue || row.SectionId.HasValue || row.EducationMediumStageId.HasValue || row.StandardId.HasValue)));

	private async Task<LibraryRolloverCalendarDto> ReadCalendarAsync(Guid targetYear)
	{
		var settings = await calendar.GetSettingsAsync();
		var confirmed = settings.Source == "Library" && !settings.NeedsConfiguration ? settings.TimeZoneId : null;
		try
		{
			var open = (await calendar.GetOpenWeekdaysAsync(targetYear)).Count;
			return new(confirmed, open, confirmed != null && open > 0, null);
		}
		catch (BusinessException exception) when (exception.Code == LibraryErrorCodes.CalendarInvalid)
		{
			return new(confirmed, null, false, exception.Message);
		}
	}

	private async Task<LibraryRolloverLoansDto> ReadLoansAsync(LibraryAcademicContext context, Guid sourceYear)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View))
			return new("Restricted", null, null);
		var schoolWide = context.UserScope.HasTenantWideScope();
		var query = (await storedLoans.GetReadableLoansAsync(context.UserScope)).Where(loan => loan.Status != BookIssueStatus.Returned &&
			(loan.AcademicYearId == sourceYear || schoolWide && !loan.AcademicYearId.HasValue));
		var counts = await AsyncExecuter.ToListAsync(query.GroupBy(loan => loan.AcademicYearId).Select(group => new { Year = group.Key, Count = group.Count() }));
		return new(schoolWide ? "School" : "Assigned", counts.SingleOrDefault(row => row.Year == sourceYear)?.Count ?? 0,
			schoolWide ? counts.SingleOrDefault(row => row.Year == null)?.Count ?? 0 : null);
	}

	private static LibraryRolloverYearDto MapYear(AcademicYear year) =>
		new(year.Id, year.Name, DateOnly.FromDateTime(year.StartDate), DateOnly.FromDateTime(year.EndDate));

	private static BusinessException Invalid() => new(LibraryErrorCodes.RolloverInvalid,
		"Choose a later target academic year and a school date within its dates.");
}
