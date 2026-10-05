using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementReadScopeService(
	IUserScopeService userScopeService,
	ICurrentUser currentUser,
	HrEmployeeScopeQueryService employeeScope,
	IAsyncQueryableExecuter executer) : ITransientDependency
{
	private const string Denied = "Access denied: This professional review is outside your assigned review scope.";

	public async Task<IQueryable<PerformanceReviewAssignment>> ApplyAsync(IQueryable<PerformanceReviewAssignment> query)
	{
		var scope = await GetScopeAsync();
		if (HrUserScopeAccess.HasHrWideAccess(scope)) return query;
		var employees = await employeeScope.BuildScopedEmployeeQueryAsync(scope);
		return query.Where(x => employees.Select(employee => employee.Id).Contains(x.EmployeeId));
	}

	public async Task EnsureEmployeeAsync(Guid employeeId)
	{
		var scope = await GetScopeAsync();
		if (HrUserScopeAccess.HasHrWideAccess(scope)) return;
		var query = await employeeScope.BuildScopedEmployeeQueryAsync(scope);
		if (!await executer.AnyAsync(query.Where(x => x.Id == employeeId))) throw new AbpAuthorizationException(Denied);
	}

	public async Task<IQueryable<PerformanceAuditEvent>> ApplyAuditAsync(
		IQueryable<PerformanceAuditEvent> query, IQueryable<PerformanceReviewAssignment> reviews)
	{
		var scope = await GetScopeAsync();
		if (HrUserScopeAccess.HasHrWideAccess(scope)) return query;
		var employees = await employeeScope.BuildScopedEmployeeQueryAsync(scope);
		var scopedReviews = reviews.Where(x => employees.Select(employee => employee.Id).Contains(x.EmployeeId));
		return query.Where(x => x.ReviewAssignmentId.HasValue && scopedReviews.Select(review => review.Id).Contains(x.ReviewAssignmentId.Value));
	}

	private async Task<UserScopeContext> GetScopeAsync()
	{
		if (!currentUser.Id.HasValue) throw new AbpAuthorizationException(Denied);
		var scope = await userScopeService.GetUserScopeAsync(currentUser.Id.Value);
		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope)) throw new AbpAuthorizationException(Denied);
		return scope;
	}
}
