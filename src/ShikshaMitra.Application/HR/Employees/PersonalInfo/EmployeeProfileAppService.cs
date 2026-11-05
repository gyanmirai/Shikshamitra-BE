using ShikshaMitra.HR.WorkShifts;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Employees.Search;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

[Authorize]
[RemoteService(false)]
public class EmployeeProfileAppService(
	IEmployeeAuthorizationService authorizationService,
	IEmployeeRepository employeeRepository,
	EmployeeOrganizationScopeResolver organizationScopeResolver, EmployeeShiftResolver shiftResolver)
	: ApplicationService, IEmployeeProfileAppService
{
	public async Task<EmployeeDetails> GetAsync(Guid employeeId)
	{
		// Keep the existing IDOR and current-scope check before reading profile data.
		await authorizationService.CheckEmployeeAccessAsync(employeeId);
		var employee = await AsyncExecuter.FirstOrDefaultAsync(
			(await employeeRepository.GetQueryableAsync())
				.Where(x => x.Id == employeeId)
				.Select(EmployeeProfileProjection.Details))
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		var shift = await shiftResolver.FindShiftAsync(employeeId, await shiftResolver.TodayAsync());
		employee.Shift = shift == null ? null : new ShiftInfo { Id = shift.Id, Name = shift.ShiftName, StartTime = shift.StartTime.ToString("HH:mm:ss"), EndTime = shift.EndTime.ToString("HH:mm:ss") };
		var organizations = await organizationScopeResolver.GetOrganizationsByUserIdsAsync([employee.UserId]);
		EmployeeSearchProjection.ApplyOrganizations(employee, organizations);
		return employee;
	}
}