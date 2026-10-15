using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance;

public partial class FinanceScopeAccessService
{
	public async Task<IQueryable<Guid>> GetScopedFeeAccountIdsQueryAsync()
	{
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var scope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		var query = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetQueryableAsync())
			.Where(a => a.TenantId == CurrentTenant.Id);
		return FinanceFeeAccountScopeQueryBuilder.ApplyScope(query, scope).Select(a => a.Id);
	}

	public async Task EnsureFeeAccountAccessibleAsync(FinanceFeeAccount account)
	{
		if (account.TenantId != CurrentTenant.Id || !await AsyncExecuter.AnyAsync((await GetScopedFeeAccountIdsQueryAsync()).Where(id => id == account.Id)))
			throw new AbpAuthorizationException();
	}

	public async Task EnsureCollectionPayerAccessibleAsync(Guid? studentId, Guid? accountId, CancellationToken cancellationToken = default)
	{
		if (accountId.HasValue)
		{
			var account = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetAsync(accountId.Value, cancellationToken: cancellationToken);
			await EnsureFeeAccountAccessibleAsync(account);
			return;
		}
		if (!studentId.HasValue || studentId == Guid.Empty) throw new AbpAuthorizationException();
		await EnsureStudentAccessibleAsync(studentId.Value, cancellationToken);
	}

	public async Task EnsureCollectionYearAccessibleAsync(Guid yearId)
	{
		var currentScope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		if (!CurrentUser.Id.HasValue || !await academicYearRepository.AnyAsync(y => y.Id == yearId && y.TenantId == CurrentTenant.Id) ||
			!(currentScope.IsAdmin || currentScope.HasWildcardAcademicYear || currentScope.HasAnyAccess && currentScope.AcademicYearIds.Contains(yearId)))
			throw new AbpAuthorizationException();
	}
}
