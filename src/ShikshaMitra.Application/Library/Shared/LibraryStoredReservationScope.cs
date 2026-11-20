using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Shared;

public class LibraryStoredReservationScope(IRepository<BookReservation, Guid> requests, IRepository<AcademicYear, Guid> years,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, LibraryRecordedStaffScopeQuery staffScope) : ITransientDependency
{
	public async Task<IQueryable<BookReservation>> GetReadableRequestsAsync(UserScopeContext scope)
	{
		var query = (await requests.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		if (!tenant.Id.HasValue || scope.TenantId != tenant.Id || !scope.HasAnyAccess) return query.Where(_ => false);
		if (scope.HasTenantWideScope()) return query;
		var allowedYears = scope.AcademicYearIds.Concat(scope.WildcardBoardAcademicYearIds).Distinct().ToArray();
		var realYears = (await years.GetQueryableAsync()).Where(year => year.TenantId == tenant.Id && Enumerable.Contains(allowedYears, year.Id));
		var wholeYears = scope.WildcardBoardAcademicYearIds.ToArray();
		var wholeBoards = scope.WildcardMediumBoardIds.ToArray();
		var wholeMediums = scope.WildcardMediumStageMediumIds.ToArray();
		var wholeStages = scope.WildcardStandardMediumStageIds.ToArray();
		var wholeClasses = scope.WildcardDivisionStandardIds.ToArray();
		var divisions = scope.DivisionIds.ToArray();
		var staffCaptures = await staffScope.GetReadableCapturesAsync(scope);
		return query.Where(row => row.AcademicYearId.HasValue &&
			realYears.Any(year => year.Id == row.AcademicYearId.Value) &&
			(row.MemberRole == 0 && (Enumerable.Contains(wholeYears, row.AcademicYearId.Value) ||
			 (row.BorrowerScopeProvenance == LibraryBorrowerScopeProvenance.CapturedAtOperation || row.BorrowerScopeProvenance == LibraryBorrowerScopeProvenance.ReviewedLegacy) &&
			 (row.BorrowerBoardId.HasValue && Enumerable.Contains(wholeBoards, row.BorrowerBoardId.Value) ||
			  row.BorrowerMediumId.HasValue && Enumerable.Contains(wholeMediums, row.BorrowerMediumId.Value) ||
			  row.BorrowerStageId.HasValue && Enumerable.Contains(wholeStages, row.BorrowerStageId.Value) ||
			  row.BorrowerStandardId.HasValue && Enumerable.Contains(wholeClasses, row.BorrowerStandardId.Value) ||
			  row.BorrowerDivisionId.HasValue && Enumerable.Contains(divisions, row.BorrowerDivisionId.Value))) ||
			 row.MemberRole == 1 && row.CanonicalMemberId.HasValue && staffCaptures.Any(capture => capture.Id == row.BorrowerScopeCaptureId &&
				capture.TenantId == row.TenantId && capture.SourceKind == LibraryBorrowerScopeSourceKind.Reservation && capture.SourceId == row.Id &&
				capture.CanonicalEmployeeId == row.CanonicalMemberId.Value && capture.AcademicYearId == row.AcademicYearId &&
				capture.Version == row.BorrowerScopeCaptureVersion && capture.Provenance == row.BorrowerScopeProvenance)));
	}

	public async Task<bool> CanReadAsync(Guid requestId, UserScopeContext scope) =>
		await queries.AnyAsync((await GetReadableRequestsAsync(scope)).Where(row => row.Id == requestId));
}
