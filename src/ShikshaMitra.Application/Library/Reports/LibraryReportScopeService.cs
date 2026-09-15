using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportScopeService(
	LibraryReportRequestGuard requestGuard,
	IUserScopeAccessService userScopeAccessService,
	IRepository<AcademicYear, Guid> academicYearRepository,
	ICurrentTenant currentTenant)
	: ITransientDependency
{
	public async Task<LibraryReportExecutionScope> AuthorizeAndNormalizeAsync(LibraryReportRequestDto request)
	{
		requestGuard.ValidateAndNormalize(request);
		var executionScope = await GetCurrentExecutionScopeAsync();
		if (request.Filters.AcademicYearId.HasValue &&
		    !executionScope.AllowedAcademicYearIds.Contains(request.Filters.AcademicYearId.Value))
		{
			throw new BusinessException(LibraryErrorCodes.ReportScopeAccessDenied)
				.WithData("AcademicYearId", request.Filters.AcademicYearId.Value);
		}

		return executionScope;
	}

	public async Task<LibraryReportExecutionScope> GetCurrentExecutionScopeAsync()
	{
		var userScope = await userScopeAccessService.GetCurrentUserScopeAsync();
		userScopeAccessService.EnsureHasAnyAccess(userScope, LibraryErrorCodes.ReportScopeAccessDenied);
		var academicYears = await academicYearRepository.GetListAsync();
		var allowedYearIds = userScope.HasTenantWideScope()
			? academicYears.Select(year => year.Id).ToList()
			: academicYears
				.Where(year => userScope.AcademicYearIds.Contains(year.Id) || userScope.WildcardBoardAcademicYearIds.Contains(year.Id))
				.Select(year => year.Id)
				.ToList();
		if (allowedYearIds.Count == 0)
		{
			throw new BusinessException(LibraryErrorCodes.ReportScopeAccessDenied);
		}

		return new LibraryReportExecutionScope
		{
			TenantId = currentTenant.Id,
			UserScope = userScope,
			AllowedAcademicYearIds = allowedYearIds
		};
	}
}
