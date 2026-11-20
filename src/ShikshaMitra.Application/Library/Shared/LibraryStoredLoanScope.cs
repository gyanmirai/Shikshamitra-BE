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

public class LibraryStoredLoanScope(IRepository<BookIssue, Guid> loans, IRepository<AcademicYear, Guid> years,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, LibraryRecordedStaffScopeQuery staffScope) : ITransientDependency
{
	public async Task<IQueryable<BookIssue>> GetReadableLoansAsync(UserScopeContext scope)
	{
		var query = (await loans.GetQueryableAsync()).Where(loan => loan.TenantId == tenant.Id);
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
		return query.Where(loan => loan.AcademicYearId.HasValue &&
			realYears.Any(year => year.Id == loan.AcademicYearId.Value) &&
			(loan.MemberRole == 0 && (Enumerable.Contains(wholeYears, loan.AcademicYearId.Value) ||
			 loan.BorrowerBoardId.HasValue && Enumerable.Contains(wholeBoards, loan.BorrowerBoardId.Value) ||
			 loan.BorrowerMediumId.HasValue && Enumerable.Contains(wholeMediums, loan.BorrowerMediumId.Value) ||
			 loan.BorrowerStageId.HasValue && Enumerable.Contains(wholeStages, loan.BorrowerStageId.Value) ||
			 loan.BorrowerStandardId.HasValue && Enumerable.Contains(wholeClasses, loan.BorrowerStandardId.Value) ||
			 loan.BorrowerDivisionId.HasValue && Enumerable.Contains(divisions, loan.BorrowerDivisionId.Value)) ||
			 loan.MemberRole == 1 && staffCaptures.Any(capture => capture.Id == loan.BorrowerScopeCaptureId &&
				capture.TenantId == loan.TenantId && capture.SourceKind == LibraryBorrowerScopeSourceKind.Loan && capture.SourceId == loan.Id &&
				capture.CanonicalEmployeeId == loan.MemberId && capture.AcademicYearId == loan.AcademicYearId &&
				capture.Version == loan.BorrowerScopeCaptureVersion && capture.Provenance == loan.BorrowerScopeProvenance)));
	}

	public async Task<bool> CanReadAsync(Guid loanId, UserScopeContext scope) =>
		await queries.AnyAsync((await GetReadableLoansAsync(scope)).Where(loan => loan.Id == loanId));
}
