using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library.Charges;

// Data-scope predicate only. Each owning entry point checks its own exact action.
public class LibraryAssessmentSourceScopeQuery(IRepository<LibraryCharge, Guid> charges,
	IRepository<LibraryMember, Guid> memberships, ILibraryAccessService access,
	LibraryStoredLoanScope storedLoans, ICurrentTenant tenant, ICurrentUser user) : ITransientDependency
{
	public async Task<IQueryable<FinanceExternalReadableSource>> GetQueryAsync(UserScopeContext scope,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var chargeQuery = (await charges.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		if (!tenant.Id.HasValue || !user.Id.HasValue || scope.TenantId != tenant.Id || scope.UserId != user.Id || !scope.HasAnyAccess)
			return NoReadableSources(chargeQuery);
		var studentIds = (await access.GetStudentsAsync(scope)).Select(row => row.Id);
		var employeeIds = (await access.GetEmployeesAsync(scope)).Select(row => row.Id);
		var loans = await storedLoans.GetReadableLoansAsync(scope);
		var aliases = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var candidates = chargeQuery.Select(charge => new
		{
			Charge = charge,
			StudentId = charge.MemberRole == 0
				? (Guid?)((aliases.Where(alias => alias.Id == charge.MemberId).Select(alias => (Guid?)alias.StudentId).FirstOrDefault()) ?? charge.MemberId)
				: null
		});
		cancellationToken.ThrowIfCancellationRequested();
		return candidates.Where(row => row.Charge.MemberRole == 0 && row.StudentId.HasValue &&
			(!row.Charge.BookIssueId.HasValue && studentIds.Contains(row.StudentId.Value) ||
			 row.Charge.BookIssueId.HasValue && loans.Any(loan => loan.Id == row.Charge.BookIssueId.Value && loan.TenantId == row.Charge.TenantId && loan.MemberRole == 0 &&
				(loan.MemberId == row.StudentId.Value || aliases.Any(alias => alias.Id == loan.MemberId && alias.StudentId == row.StudentId.Value)))) ||
			(row.Charge.MemberRole == 1 || row.Charge.MemberRole == 2) &&
			(!row.Charge.BookIssueId.HasValue && employeeIds.Contains(row.Charge.MemberId) ||
			 row.Charge.BookIssueId.HasValue && loans.Any(loan => loan.Id == row.Charge.BookIssueId.Value && loan.TenantId == row.Charge.TenantId && loan.MemberRole == 1 &&
				loan.MemberId == row.Charge.MemberId)))
			.Select(row => new FinanceExternalReadableSource
			{
				SourceId = row.Charge.Id, StudentId = row.StudentId,
				EmployeeId = row.Charge.MemberRole == 1 || row.Charge.MemberRole == 2 ? (Guid?)row.Charge.MemberId : null,
				AcademicYearId = row.Charge.AcademicYearId
			});
	}

	// Even a denied scope is composed into Finance joins. Bind every member that
	// those queries inspect so EF can translate the empty branch without exposing rows.
	internal static IQueryable<FinanceExternalReadableSource> NoReadableSources(IQueryable<LibraryCharge> charges) =>
		charges.Where(_ => false).Select(row => new FinanceExternalReadableSource
		{
			SourceId = row.Id,
			AcademicYearId = row.AcademicYearId,
			StudentId = null,
			EmployeeId = null
		});
}
