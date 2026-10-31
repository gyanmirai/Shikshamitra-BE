using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Users;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.HR.Employees.ReportingManager;

[RemoteService(false)]
public class ReportingManagerAppService(
	IEmployeeRepository employeeRepository,
	EmployeeReportingManager reportingManager,
	ICurrentUser currentUser,
	IEmployeeAuthorizationService employeeAuthorizationService,
	IUserScopeService userScopeService,
	IReportingManagerAccessService reportingManagerAccessService,
	TenantAdministratorReportingManagerReader administratorReader,
	Search.EmployeeSearchReader searchReader)
	: ApplicationService, IReportingManagerAppService
{
	private const int MaxHierarchyDepth = 64;

	[Microsoft.AspNetCore.Authorization.Authorize(Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<List<EmployeeBasicDto>> GetManagerOptionsAsync(string? search = null)
	{
		var employees = await searchReader.GetFilteredListAsync(new GetEmployeeListInput
		{
			Status = "Active", MaxResultCount = 500, Search = search
		});
		var options = employees.Items.Select(employee => new EmployeeBasicDto
		{
			Id = employee.Id, FullName = employee.FullName, EmployeeCode = employee.EmployeeIdentifier,
			Status = employee.Status
		}).ToList();
		var administrators = (await administratorReader.GetEligibleAsync()).Select(ReportingManagerProjection.ToAdministrator);
		if (!string.IsNullOrWhiteSpace(search))
			administrators = administrators.Where(admin => admin.FullName!.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
				(admin.Email?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false));
		options.AddRange(administrators);
		return options;
	}

	public async Task<EmployeeReportingResponseDto> AssignReportingManagerAsync(
		Guid employeeId,
		AssignReportingManagerDto input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		if (input.ReportingManagerKind == "TenantAdministrator")
			await reportingManager.AssignAdministratorAsync(employeeId, input.ReportingManagerId);
		else if (input.ReportingManagerKind == "Employee")
			await reportingManager.AssignReportingManagerAsync(employeeId, input.ReportingManagerId);
		else
			throw new BusinessException(HRErrorCodes.ReportingManagerMustBeActive);
		var affectedUserIds = (await employeeRepository.GetListAsync())
			.Where(employee => employee.UserId.HasValue)
			.Select(employee => employee.UserId!.Value)
			.ToList();
		foreach (var affectedUserId in affectedUserIds)
		{
			await userScopeService.InvalidateUserScopeCacheAsync(affectedUserId);
		}
		await reportingManagerAccessService.InvalidateAsync(affectedUserIds);
		var updatedEmployee = await employeeRepository.GetWithReportingManagerAsync(employeeId);

		return ReportingManagerProjection.ToReportingResponse(updatedEmployee!);
	}

	public async Task<EmployeeReportingResponseDto> GetReportingManagerAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeAccessAsync(employeeId);

		var employee = await employeeRepository.GetWithReportingManagerAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		return ReportingManagerProjection.ToReportingResponse(employee);
	}

	public async Task<EmployeesByManagerResponseDto> GetEmployeesByManagerAsync(Guid managerId)
	{
		if (!await IsAdminBypassAsync())
		{
			var actorEmployee = await ResolveCurrentEmployeeAsync();
			if (actorEmployee == null || actorEmployee.Id != managerId)
			{
				throw new AbpAuthorizationException("Access denied: You can only view your own reporting hierarchy.");
			}
		}

		var manager = await employeeRepository.GetAsync(managerId)
			?? throw new EntityNotFoundException(typeof(Employee), managerId);

		var employees = await GetEmployeesByManagerHierarchyAsync(managerId);

		return ReportingManagerProjection.ToEmployeesByManagerResponse(manager, employees);
	}

	public async Task<EmployeesByManagerResponseDto> GetMyEmployeesByManagerAsync()
	{
		var actorEmployee = await ResolveCurrentEmployeeAsync()
			?? throw new AbpAuthorizationException("Access denied: Staff profile not found.");

		return await GetEmployeesByManagerAsync(actorEmployee.Id);
	}

	private async Task<List<Employee>> GetEmployeesByManagerHierarchyAsync(Guid managerId)
	{
		var result = new List<Employee>();
		var visited = new HashSet<Guid> { managerId };
		var queue = new Queue<Guid>();
		queue.Enqueue(managerId);
		var depth = 0;

		while (queue.Count > 0 && depth < MaxHierarchyDepth)
		{
			depth++;
			var currentManager = queue.Dequeue();
			var directReports = await employeeRepository.GetEmployeesByManagerAsync(currentManager);

			foreach (var employee in directReports)
			{
				if (!visited.Add(employee.Id))
				{
					continue;
				}

				result.Add(employee);
				queue.Enqueue(employee.Id);
			}
		}

		return result;
	}

	private async Task<Employee?> ResolveCurrentEmployeeAsync()
	{
		var userId = currentUser.Id;
		if (!userId.HasValue)
		{
			return null;
		}

		return await employeeRepository.GetEmployeeByUserIdAsync(userId.Value);
	}

	private Task<bool> IsAdminBypassAsync()
	{
		return Task.FromResult(currentUser.IsInRole("admin") || currentUser.IsInRole("Administrator"));
	}
}
