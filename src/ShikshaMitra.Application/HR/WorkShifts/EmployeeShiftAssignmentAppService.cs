using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using H = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR;

namespace ShikshaMitra.HR.WorkShifts;

[RemoteService(false)]
[Authorize]
public class EmployeeShiftAssignmentAppService(IEmployeeRepository employees, IEmployeeAuthorizationService authorization, IRepository<EmployeeShiftAssignment, Guid> assignments, EmployeeShiftAssignmentManager manager, EmployeeShiftResolver resolver) : ApplicationService, IEmployeeShiftAssignmentAppService
{
	[Authorize(H.Employee_EmployeeList.View)]
	public async Task<EmployeeWorkScheduleDto> GetAsync(Guid employeeId)
	{
		await authorization.CheckEmployeeAccessAsync(employeeId);
		return await ReadAsync(employeeId);
	}
	[Authorize(H.Employee_EmployeeList.Edit)]
	public async Task<EmployeeWorkScheduleDto> ScheduleAsync(Guid employeeId, ScheduleEmployeeShiftInput input)
	{
		await authorization.CheckEmployeeModifyAccessAsync(employeeId);
		await manager.ScheduleAsync(await employees.GetAsync(employeeId), input.WorkShiftId, input.EffectiveFrom, input.ConcurrencyStamp);
		return await ReadAsync(employeeId);
	}
	[Authorize(H.Employee_EmployeeList.Edit)]
	public async Task<EmployeeWorkScheduleDto> CancelAsync(Guid employeeId, Guid assignmentId, string concurrencyStamp)
	{
		await authorization.CheckEmployeeModifyAccessAsync(employeeId);
		await manager.CancelAsync(await employees.GetAsync(employeeId), assignmentId, concurrencyStamp);
		return await ReadAsync(employeeId);
	}
	private async Task<EmployeeWorkScheduleDto> ReadAsync(Guid employeeId)
	{
		var employee = await employees.GetAsync(employeeId);
		var today = await resolver.TodayAsync();
		var rows = (await assignments.GetListAsync(x => x.EmployeeId == employeeId && x.TenantId == CurrentTenant.Id)).OrderBy(x => x.EffectiveFrom).ToList();
		return new() { Today = today, ConcurrencyStamp = employee.ConcurrencyStamp, Current = rows.Where(x => x.Covers(today)).Select(Map).SingleOrDefault(), Upcoming = rows.Where(x => x.EffectiveFrom > today).Select(Map).ToList(), History = rows.Where(x => x.EffectiveTo.HasValue && x.EffectiveTo <= today).Select(Map).ToList() };
	}
	internal static EmployeeShiftAssignmentDto Map(EmployeeShiftAssignment x) => new() { Id = x.Id, WorkShiftId = x.WorkShiftId, EffectiveFrom = x.EffectiveFrom, EffectiveTo = x.EffectiveTo, ShiftName = x.ShiftName, StartTime = x.StartTime, EndTime = x.EndTime, Provenance = x.Provenance, ConcurrencyStamp = x.ConcurrencyStamp };
}