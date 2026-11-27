using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library.Charges;

[ExposeServices(typeof(IFinanceExternalSourceScope))]
public class LibraryFinanceSourceScope(IRepository<LibraryCharge, Guid> charges,
	LibraryAssessmentSourceScopeQuery sourceScope, IUserScopeAccessService scopeAccess,
	IPermissionChecker permissions, ICurrentTenant tenant, ICurrentUser user)
	: IFinanceExternalSourceScope, ITransientDependency
{
	public async Task<IQueryable<FinanceExternalReadableSource>> GetReadableSourcesAsync(FinanceExternalSourceKind kind,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var chargeQuery = (await charges.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var empty = LibraryAssessmentSourceScopeQuery.NoReadableSources(chargeQuery);
		if (kind != FinanceExternalSourceKind.LibraryAssessment || !tenant.Id.HasValue || !user.Id.HasValue ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)) return empty;
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		if (scope.TenantId != tenant.Id || scope.UserId != user.Id || !scope.HasAnyAccess) return empty;
		return await sourceScope.GetQueryAsync(scope, cancellationToken);
	}
}
