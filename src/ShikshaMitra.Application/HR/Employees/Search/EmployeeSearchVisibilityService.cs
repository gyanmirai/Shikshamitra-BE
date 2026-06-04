using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.Search;

[RemoteService(false)]
public class EmployeeSearchVisibilityService(IEmployeeRepository employeeRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<IQueryable<Employee>> ApplyVisibilityScopeAsync(IQueryable<Employee> queryable)
	{
		if (IsPrivilegedUser())
		{
			return queryable;
		}

		var currentUserId = CurrentUser.Id;
		if (!currentUserId.HasValue)
		{
			return queryable.Where(_ => false);
		}

		var actorEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId.Value);
		if (actorEmployee == null)
		{
			return queryable.Where(_ => false);
		}

		var reporteeIds = await GetDescendantReporteeIdsAsync(actorEmployee.Id);
		if (reporteeIds.Count == 0)
		{
			return queryable.Where(e => e.Id == actorEmployee.Id);
		}

		return queryable.Where(e => reporteeIds.Contains(e.Id));
	}

	private async Task<List<Guid>> GetDescendantReporteeIdsAsync(Guid managerId)
	{
		var result = new List<Guid>();
		var visited = new HashSet<Guid> { managerId };
		var managerQueue = new Queue<Guid>();
		managerQueue.Enqueue(managerId);

		while (managerQueue.Count > 0)
		{
			var currentManagerId = managerQueue.Dequeue();
			var reportees = await employeeRepository.GetEmployeesByManagerAsync(currentManagerId);

			foreach (var reportee in reportees)
			{
				if (!visited.Add(reportee.Id))
				{
					continue;
				}

				result.Add(reportee.Id);
				managerQueue.Enqueue(reportee.Id);
			}
		}

		return result;
	}

	private bool IsPrivilegedUser()
	{
		return CurrentUser.IsInRole("admin")
			|| CurrentUser.IsInRole("Administrator")
			|| CurrentUser.IsInRole("HR Admin")
			|| CurrentUser.IsInRole("HR Manager");
	}
}
