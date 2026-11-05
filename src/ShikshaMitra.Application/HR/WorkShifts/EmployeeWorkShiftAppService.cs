using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization.Permissions;
using H = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR;

namespace ShikshaMitra.HR.WorkShifts;

[RemoteService(false)]
[Authorize]
public class EmployeeWorkShiftAppService(IRepository<EmployeeWorkShift, Guid> shifts, IRepository<EmployeeShiftAssignment, Guid> assignments, IPermissionChecker permissions, EmployeeShiftResolver resolver, IDataFilter filters) : ApplicationService, IEmployeeWorkShiftAppService
{
	[Authorize(H.AttendanceAndLeave_Setting_ShiftManagement.View)]
	public async Task<PagedResultDto<EmployeeWorkShiftDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var rows = (await shifts.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id);
		var descending = input.Sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var key = input.Sorting?.Split(' ', ',')[0].ToLowerInvariant();
		IOrderedQueryable<EmployeeWorkShift> ordered = key switch
		{
			"starttime" => descending ? rows.OrderByDescending(x => x.StartTime) : rows.OrderBy(x => x.StartTime),
			"endtime" => descending ? rows.OrderByDescending(x => x.EndTime) : rows.OrderBy(x => x.EndTime),
			_ => descending ? rows.OrderByDescending(x => x.ShiftName) : rows.OrderBy(x => x.ShiftName)
		};
		var count = await AsyncExecuter.CountAsync(rows);
		var page = await AsyncExecuter.ToListAsync(ordered.ThenBy(x => x.Id).Skip(input.SkipCount).Take(input.MaxResultCount));
		var ids = page.Select(x => x.Id).ToArray();
		using var historyFilter = filters.Disable<ISoftDelete>();
		var referenced = (await assignments.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, x.WorkShiftId))).Select(x => x.WorkShiftId).ToHashSet();
		return new(count, page.Select(x => Map(x, referenced.Contains(x.Id))).ToList());
	}
	public async Task<WorkShiftOptionsDto> LookupAsync()
	{
		if (!await permissions.IsGrantedAsync(H.Employee_EmployeeList.Add) && !await permissions.IsGrantedAsync(H.Employee_EmployeeList.Edit) && !await permissions.IsGrantedAsync(H.AttendanceAndLeave_Setting_ShiftManagement.View)) throw new AbpAuthorizationException();
		return new() { Today = await resolver.TodayAsync(), Items = (await shifts.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.IsActive)).OrderBy(x => x.ShiftName).Select(x => Map(x, false)).ToList() };
	}
	[Authorize(H.AttendanceAndLeave_Setting_ShiftManagement.Add)]
	public async Task<EmployeeWorkShiftDto> CreateAsync(SaveWorkShiftInput input)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(HRErrorCodes.WorkShiftInvalid, "Work shifts require a school tenant.");
		var shift = new EmployeeWorkShift(GuidGenerator.Create(), CurrentTenant.Id, input.ShiftName, input.StartTime, input.EndTime);
		shift.Update(input.ShiftName, input.StartTime, input.EndTime, input.IsActive);
		return Map(await shifts.InsertAsync(shift, autoSave: true), false);
	}
	[Authorize(H.AttendanceAndLeave_Setting_ShiftManagement.Add)]
	public async Task<EmployeeWorkShiftDto> UpdateAsync(Guid id, SaveWorkShiftInput input)
	{
		var shift = await shifts.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
		CheckStamp(shift, input.ConcurrencyStamp);
		var referenced = await IsReferencedAsync(id);
		if (referenced && (shift.StartTime != input.StartTime || shift.EndTime != input.EndTime))
			throw new BusinessException(HRErrorCodes.WorkShiftReferenced, "Assigned shift times are immutable. Create a new work shift.");
		shift.Update(input.ShiftName, input.StartTime, input.EndTime, input.IsActive);
		return Map(await shifts.UpdateAsync(shift, autoSave: true), referenced);
	}
	[Authorize(H.AttendanceAndLeave_Setting_ShiftManagement.Delete)]
	public async Task DeleteAsync(Guid id, string concurrencyStamp)
	{
		var shift = await shifts.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
		CheckStamp(shift, concurrencyStamp);
		// Take the same write lock as scheduling before checking dependencies.
		await shifts.UpdateAsync(shift, autoSave: true);
		if (await IsReferencedAsync(id)) throw new BusinessException(HRErrorCodes.WorkShiftReferenced, "This work shift has assignment history and cannot be deleted.");
		await shifts.DeleteAsync(shift, autoSave: true);
	}
	private async Task<bool> IsReferencedAsync(Guid id)
	{ using (filters.Disable<ISoftDelete>()) return await assignments.AnyAsync(x => x.WorkShiftId == id && x.TenantId == CurrentTenant.Id); }
	private static void CheckStamp(EmployeeWorkShift shift, string? stamp)
	{ if (string.IsNullOrWhiteSpace(stamp) || shift.ConcurrencyStamp != stamp) throw new AbpDbConcurrencyException("The shift changed. Refresh and try again."); }
	internal static EmployeeWorkShiftDto Map(EmployeeWorkShift x, bool assigned) => new() { Id = x.Id, ShiftName = x.ShiftName, StartTime = x.StartTime, EndTime = x.EndTime, IsActive = x.IsActive, IsAssigned = assigned, ConcurrencyStamp = x.ConcurrencyStamp };
}