using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Custody;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library.Clearance;

public sealed record LibraryStaffClearanceEvidence(string Status, int? OutstandingLoanCount);

/// <summary>Physical loan evidence for staff already authorized by the Employee Records owner.</summary>
public class LibraryStaffClearanceReader(ICurrentTenant tenant, ICurrentUser user, IPermissionChecker permissions,
	IUserScopeAccessService scopeAccess, LibraryStoredLoanScope loans, IAsyncQueryableExecuter queries,
	IRepository<LibraryCustodyHandoff, Guid> custodyHandoffs, IRepository<LibraryCustodyItem, Guid> custodyItems) : ITransientDependency
{
	public async Task<LibraryStaffClearanceEvidence> ReadAsync(Guid employeeId)
	{
		if (tenant.Id is not Guid school || school == Guid.Empty || employeeId == Guid.Empty ||
			!user.IsAuthenticated || user.Id is not Guid actor || actor == Guid.Empty || user.TenantId != school ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View))
			return new("Restricted", null);

		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		if (scope.TenantId != school || !scope.HasAnyAccess) return new("Restricted", null);
		var permitted = await loans.GetReadableLoansAsync(scope);
		var outstanding = await queries.CountAsync(permitted.Where(loan => loan.MemberRole == 1 &&
			loan.MemberId == employeeId && loan.Status != BookIssueStatus.Returned));
		if (!scope.HasTenantWideScope()) return new("Limited", outstanding);
		return new(outstanding > 0 ? "Pending" : "Clear", outstanding);
	}

	internal async Task<int?> ReadCustodyCountAsync(Guid employeeId)
	{
		if (tenant.Id is not Guid school || school == Guid.Empty || employeeId == Guid.Empty ||
			!user.IsAuthenticated || user.Id is not Guid actor || actor == Guid.Empty || user.TenantId != school ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Custody.View) ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View))
			return null;
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		if (scope.TenantId != school || !scope.HasTenantWideScope()) return null;
		var activeHandoffs = (await custodyHandoffs.GetQueryableAsync()).Where(row => row.TenantId == school && row.EmployeeId == employeeId);
		var activeCopies = (await custodyItems.GetQueryableAsync()).Where(row => row.TenantId == school && !row.ReturnReceiptId.HasValue &&
			activeHandoffs.Any(handoff => handoff.Id == row.HandoffId));
		return await queries.CountAsync(activeCopies);
	}
}
