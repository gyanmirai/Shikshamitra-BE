using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardReportingManagerIssueReader(IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	internal async Task<InactiveReportingManagerIssue> BuildInactiveReportingManagerIssueAsync(
		IQueryable<Employee> employeeQuery)
	{
		var affectedRows = await asyncExecuter.ToListAsync(
			employeeQuery
				.Where(e =>
					e.EmployeeStatusType == EmployeeStatusType.Active &&
					e.ReportingManagerId.HasValue &&
					e.ReportingManager != null &&
					e.ReportingManager.EmployeeStatusType.HasValue &&
					e.ReportingManager.EmployeeStatusType.Value != EmployeeStatusType.Active)
				.Select(e => new
				{
					e.Id,
					ManagerId = e.ReportingManagerId!.Value
				}));

		return new InactiveReportingManagerIssue(
			affectedRows.Count,
			affectedRows.Select(x => x.ManagerId).Distinct().Count());
	}
}
