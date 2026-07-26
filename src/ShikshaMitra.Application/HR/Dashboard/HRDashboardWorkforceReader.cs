using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardWorkforceReader(IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<HRDashboardWorkforceDto> BuildWorkforceAsync(
		IQueryable<Employee> activeEmployees,
		int totalActive,
		int teaching,
		int nonTeaching,
		int newJoiners,
		int inactive,
		DateTime selectedMonthStart,
		DateTime selectedNextMonthStart)
	{
		var recentEmployees = await asyncExecuter.ToListAsync(activeEmployees
			.OrderByDescending(e => e.CreationTime)
			.Take(6)
			.Select(e => new HRDashboardEmployeeSnapshotDto
			{
				Id = e.Id,
				Name = e.EmployeeName,
				EmployeeIdentifier = e.EmployeeCode ?? string.Empty,
				RoleName = e.Role != null ? e.Role.Name : null,
				Department = e.Department,
				IsTeaching = e.EmployeeGroup == EmployeeGroupType.Teaching,
				DateOfJoining = e.DateOfJoining
			}));
		var monthStart = DateOnly.FromDateTime(selectedMonthStart);
		var nextMonthStart = DateOnly.FromDateTime(selectedNextMonthStart);
		var newJoinerEmployees = await asyncExecuter.ToListAsync(activeEmployees
			.Where(e =>
				e.DateOfJoining.HasValue &&
				e.DateOfJoining.Value >= monthStart &&
				e.DateOfJoining.Value < nextMonthStart)
			.OrderByDescending(e => e.DateOfJoining)
			.ThenBy(e => e.FirstName)
			.ThenBy(e => e.LastName)
			.ThenBy(e => e.Id)
			.Take(50)
			.Select(e => new HRDashboardEmployeeSnapshotDto
			{
				Id = e.Id,
				Name = e.EmployeeName,
				EmployeeIdentifier = e.EmployeeCode ?? string.Empty,
				RoleName = e.Role != null ? e.Role.Name : null,
				Department = e.Department,
				IsTeaching = e.EmployeeGroup == EmployeeGroupType.Teaching,
				DateOfJoining = e.DateOfJoining
			}));

		return new HRDashboardWorkforceDto
		{
			TotalActiveStaff = totalActive,
			TeachingStaff = teaching,
			NonTeachingStaff = nonTeaching,
			NewJoinersThisMonth = newJoiners,
			InactiveStaff = inactive,
			RecentEmployees = recentEmployees,
			NewJoiners = newJoinerEmployees
		};
	}
}
