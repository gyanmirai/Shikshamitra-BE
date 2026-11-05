using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Common;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance;

public partial class FinanceScopeAccessService
{
	public async Task<IQueryable<Guid>> GetScopedFeeAccountIdsQueryAsync()
	{
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var identity = await GetActiveIdentityAsync(CurrentUser.Id.Value);
		var scope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		var query = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetQueryableAsync())
			.Where(a => a.TenantId == CurrentTenant.Id);
		query = FinanceFeeAccountScopeQueryBuilder.ApplyScope(query, scope);
		if (!scope.IsAdmin)
		{
			var employee = await GetCurrentTenantEmployeeAsync(CurrentUser.Id.Value, default);
			if (employee != null && employee.EmployeeStatusType != EmployeeStatusType.Active)
				return query.Where(_ => false).Select(a => a.Id);
			if (await HasClassTeacherHistoryAsync(identity.Id, employee, default))
			{
				var studentIds = await GetScopedStudentIdsQueryAsync();
				query = FinanceFeeAccountScopeQueryBuilder.ApplyLinkedStudentCap(query, studentIds);
			}
		}
		return query.Select(a => a.Id);
	}

	public async Task EnsureFeeAccountAccessibleAsync(FinanceFeeAccount account)
	{
		if (account.TenantId != CurrentTenant.Id || !await AsyncExecuter.AnyAsync((await GetScopedFeeAccountIdsQueryAsync()).Where(id => id == account.Id)))
			throw new AbpAuthorizationException();
		if (account.StudentId.HasValue)
			await EnsureStudentAccessibleAsync(account.StudentId.Value);
	}

	public async Task EnsureCollectionPayerAccessibleAsync(Guid? studentId, Guid? accountId, CancellationToken cancellationToken = default)
	{
		if (studentId.HasValue && studentId.Value != Guid.Empty)
			await EnsureStudentAccessibleAsync(studentId.Value, cancellationToken);
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
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		await GetActiveIdentityAsync(CurrentUser.Id.Value);
		var currentScope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		if (!await academicYearRepository.AnyAsync(y => y.Id == yearId && y.TenantId == CurrentTenant.Id) ||
			!(currentScope.IsAdmin || currentScope.HasWildcardAcademicYear || currentScope.HasAnyAccess && currentScope.AcademicYearIds.Contains(yearId)))
			throw new AbpAuthorizationException();
	}
}
