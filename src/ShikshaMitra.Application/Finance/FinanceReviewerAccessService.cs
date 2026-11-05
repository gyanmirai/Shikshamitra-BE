using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Identity;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance;

[RemoteService(false)]
public class FinanceReviewerAccessService(
	IRepository<Employee, Guid> employeeRepository,
	FinanceScopeAccessService scopeAccess,
	IUserScopeService userScopeService,
	HrReportingAccess identities,
	IPermissionChecker permissions,
	IUnitOfWorkManager unitOfWorkManager,
	IIdentityUserRepository identityUsers,
	IRepository<IdentityUser, Guid> identityUserQueries,
	IDataFilter dataFilter) : ApplicationService, ITransientDependency
{
	private const int MaxReportingDepth = 64;

	public async Task<IReadOnlyCollection<Guid>> GetReportingMakerUserIdsAsync(
		Guid actorId, CancellationToken cancellationToken = default)
	{
		if (!CurrentTenant.Id.HasValue || actorId == Guid.Empty) return [];
		var employees = await GetReviewerEmployeesAsync(cancellationToken);
		var actorMatches = employees.Where(employee => employee.UserId == actorId).Take(2).ToArray();
		if (actorMatches.Length != 1 || actorMatches[0].Status != EmployeeStatusType.Active) return [];
		var byManager = employees.Where(employee => employee.ReportingManagerId.HasValue)
			.ToLookup(employee => employee.ReportingManagerId!.Value);
		var visited = new HashSet<Guid> { actorMatches[0].Id };
		var pending = new Queue<(Guid Id, int Depth)>();
		pending.Enqueue((actorMatches[0].Id, 0));
		var userIds = new HashSet<Guid>();
		while (pending.TryDequeue(out var current))
		{
			if (current.Depth >= MaxReportingDepth) continue;
			foreach (var reportee in byManager[current.Id])
			{
				if (!visited.Add(reportee.Id)) continue;
				pending.Enqueue((reportee.Id, current.Depth + 1));
				if (reportee.Status == EmployeeStatusType.Active && reportee.UserId.HasValue)
					userIds.Add(reportee.UserId.Value);
			}
		}
		if (userIds.Count == 0) return [];
		var ids = userIds.ToArray();
		return await AsyncExecuter.ToListAsync((await identityUserQueries.GetQueryableAsync())
			.Where(user => user.TenantId == CurrentTenant.Id && user.IsActive &&
				Enumerable.Contains(ids, user.Id)).Select(user => user.Id), cancellationToken);
	}

	public async Task<IQueryable<Guid>> GetNonEmployeeMakerIdsQueryAsync(
		CancellationToken cancellationToken = default)
	{
		if (!CurrentTenant.Id.HasValue) throw new AbpAuthorizationException("A school workspace is required.");
		Guid[] employeeUserIds;
		using (dataFilter.Disable<ISoftDelete>())
		{
			employeeUserIds = (await AsyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
				.Where(employee => employee.TenantId == CurrentTenant.Id && employee.UserId.HasValue)
				.Select(employee => employee.UserId!.Value), cancellationToken)).Distinct().ToArray();
		}
		return (await identityUserQueries.GetQueryableAsync()).Where(user =>
			user.TenantId == CurrentTenant.Id && user.IsActive &&
			!Enumerable.Contains(employeeUserIds, user.Id)).Select(user => user.Id);
	}

	public async Task<Guid?> FindEligibleReviewerAsync(
		Guid makerUserId, Guid studentId, Guid academicYearId,
		string viewPermission, string decisionPermission, CancellationToken cancellationToken = default)
	{
		if (!CurrentTenant.Id.HasValue || makerUserId == Guid.Empty || studentId == Guid.Empty ||
			academicYearId == Guid.Empty || string.IsNullOrWhiteSpace(viewPermission) ||
			string.IsNullOrWhiteSpace(decisionPermission)) return null;

		var employees = await GetReviewerEmployeesAsync(cancellationToken);
		var makerMatches = employees.Where(employee => employee.UserId == makerUserId).Take(2).ToArray();
		if (makerMatches.Length == 0)
		{
			var familyMaker = await identityUsers.FindAsync(makerUserId);
			return familyMaker is { IsActive: true } && familyMaker.TenantId == CurrentTenant.Id
				? Guid.Empty : null;
		}
		if (makerMatches.Length != 1) return null;
		var maker = makerMatches[0];

		var byId = employees.ToDictionary(employee => employee.Id);
		var visited = new HashSet<Guid> { maker.Id };
		var managerId = maker.ReportingManagerId;
		for (var depth = 0; managerId.HasValue && depth < MaxReportingDepth; depth++)
		{
			if (!visited.Add(managerId.Value) || !byId.TryGetValue(managerId.Value, out var manager)) return null;
			if (manager.UserId.HasValue && manager.UserId.Value != makerUserId &&
				manager.Status == EmployeeStatusType.Active &&
				await IsEligibleCheckerAsync(manager.UserId.Value, studentId, academicYearId,
					viewPermission, decisionPermission, cancellationToken))
				return manager.UserId.Value;
			managerId = manager.ReportingManagerId;
			if (!managerId.HasValue && manager.ReportingManagerUserId.HasValue &&
				manager.ReportingManagerUserId.Value != makerUserId &&
				await userScopeService.IsAdminUserAsync(manager.ReportingManagerUserId.Value, cancellationToken) &&
				await IsEligibleCheckerAsync(manager.ReportingManagerUserId.Value, studentId, academicYearId,
					viewPermission, decisionPermission, cancellationToken))
				return manager.ReportingManagerUserId.Value;
		}
		if (!maker.ReportingManagerId.HasValue && maker.ReportingManagerUserId.HasValue &&
			maker.ReportingManagerUserId.Value != makerUserId &&
			await userScopeService.IsAdminUserAsync(maker.ReportingManagerUserId.Value, cancellationToken) &&
			await IsEligibleCheckerAsync(maker.ReportingManagerUserId.Value, studentId, academicYearId,
				viewPermission, decisionPermission, cancellationToken))
			return maker.ReportingManagerUserId.Value;
		return null;
	}

	public async Task EnsureReviewerCanDecideAsync(
		Guid makerUserId, Guid? routedReviewerUserId, Guid studentId, Guid academicYearId,
		string viewPermission, string decisionPermission, CancellationToken cancellationToken = default)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue || CurrentUser.Id == makerUserId)
			throw new AbpAuthorizationException("An independent Finance reviewer is required.");
		var actorId = CurrentUser.Id.Value;
		if (!await IsEligibleCheckerAsync(actorId, studentId, academicYearId,
			viewPermission, decisionPermission, cancellationToken))
			throw new AbpAuthorizationException("Finance reviewer access denied.");
		// A granted school administrator may decide any other maker's request in this tenant.
		if (await userScopeService.IsAdminUserAsync(actorId, cancellationToken)) return;
		if (routedReviewerUserId == Guid.Empty)
		{
			if (await FindEligibleReviewerAsync(makerUserId, studentId, academicYearId,
				viewPermission, decisionPermission, cancellationToken) != Guid.Empty)
				throw new AbpAuthorizationException("Finance reviewer route has changed.");
			return;
		}

		if (!routedReviewerUserId.HasValue)
		{
			if (!await userScopeService.IsAdminUserAsync(actorId, cancellationToken) ||
				await FindEligibleReviewerAsync(makerUserId, studentId, academicYearId,
					viewPermission, decisionPermission, cancellationToken) != null)
				throw new AbpAuthorizationException("Finance request needs reviewer routing.");
			return;
		}
		if (routedReviewerUserId.Value != actorId ||
			await FindEligibleReviewerAsync(makerUserId, studentId, academicYearId,
				viewPermission, decisionPermission, cancellationToken) != actorId)
			throw new AbpAuthorizationException("Finance reviewer route has changed.");
	}

	private async Task<bool> IsEligibleCheckerAsync(
		Guid userId, Guid studentId, Guid academicYearId,
		string viewPermission, string decisionPermission, CancellationToken cancellationToken)
	{
		try
		{
			var principal = await identities.PrincipalAsync(userId);
			if (!await permissions.IsGrantedAsync(principal, viewPermission) ||
				!await permissions.IsGrantedAsync(principal, decisionPermission)) return false;
			var scoped = await scopeAccess.GetRequestScopedStudentsQueryForUserAsync(
				userId, academicYearId, cancellationToken, capTeacherAssignments: false);
			return await AsyncExecuter.AnyAsync(scoped.Where(student => student.Id == studentId), cancellationToken);
		}
		catch (AbpAuthorizationException) { return false; }
	}

	private async Task<List<ReviewerEmployee>> GetReviewerEmployeesAsync(CancellationToken cancellationToken)
	{
		var key = $"FinanceReviewerHierarchy:{CurrentTenant.Id}";
		var items = unitOfWorkManager.Current?.Items;
		if (items?.TryGetValue(key, out var cached) == true && cached is List<ReviewerEmployee> snapshot)
			return snapshot;
		var employees = await AsyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
			.Where(employee => employee.TenantId == CurrentTenant.Id)
			.Select(employee => new ReviewerEmployee(employee.Id, employee.UserId,
				employee.ReportingManagerId, employee.ReportingManagerUserId, employee.EmployeeStatusType)), cancellationToken);
		if (items != null) items[key] = employees;
		return employees;
	}

	private sealed record ReviewerEmployee(
		Guid Id, Guid? UserId, Guid? ReportingManagerId,
		Guid? ReportingManagerUserId, EmployeeStatusType? Status);
}
