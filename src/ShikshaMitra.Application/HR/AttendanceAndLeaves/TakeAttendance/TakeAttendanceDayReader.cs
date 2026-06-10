using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class TakeAttendanceDayReader(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IEmployeeLeaveRepository employeeLeaveRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	TakeAttendanceAccessService accessService,
	TakeAttendanceHolidayReader holidayReader,
	TakeAttendanceEmployeeStageResolver employeeStageResolver,
	HrUserScopeAccessService hrScopeAccessService,
	HrEmployeeScopeQueryService hrEmployeeScopeQueryService,
	IRepository<ShiftManagement, Guid> shiftRepository,
	AttendanceCaptureModeLookupResolver captureModeLookupResolver,
	IClock clock,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<TakeAttendanceDayResponse> GetDayAsync(GetTakeAttendanceDayInput input)
	{
		var date = TakeAttendanceDateParser.ParseDate(input.AttendanceDate);
		var dateOnly = DateOnly.FromDateTime(date);
		if (date.Date > clock.Now.Date)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceFutureDateNotAllowed);
		}

		var scope = await accessService.EnsureCanOperateAttendanceAsync();
		var locked = await staffAttendanceDayLockRepository.FindAsync(dateOnly);
		var holiday = await holidayReader.FindHolidayAsync(dateOnly);
		var holidayReason = holiday == null ? null : $"Holiday: {holiday.HolidayName}";

		var empQuery = await hrEmployeeScopeQueryService.BuildScopedEmployeeQueryAsync(
			scope,
			new HrEmployeeScopeOptions
			{
				ActiveOnOrAfter = dateOnly,
				JoinedOnOrBefore = dateOnly
			});

		if (!string.IsNullOrWhiteSpace(input.Department))
		{
			var d = input.Department.Trim();
			empQuery = empQuery.Where(e => e.Department != null && e.Department.Contains(d));
		}

		if (input.IsTeaching.HasValue)
		{
			empQuery = input.IsTeaching.Value
				? empQuery.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching)
				: empQuery.Where(e => e.EmployeeGroup != EmployeeGroupType.Teaching);
		}

		if (input.RoleId.HasValue)
		{
			empQuery = empQuery.Where(e => e.RoleId == input.RoleId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var s = input.Search.Trim().ToLowerInvariant();
			empQuery = empQuery.Where(e =>
				((e.FirstName ?? "") + " " + (e.MiddleName ?? "") + " " + (e.LastName ?? "")).ToLower().Contains(s) ||
				(e.EmployeeCode ?? "").ToLower().Contains(s) ||
				(e.MobileNo ?? "").ToLower().Contains(s));
		}

		empQuery = empQuery.OrderBy(e => e.FirstName).ThenBy(e => e.LastName);

		var allEmployeeIds = await asyncExecuter.ToListAsync(empQuery.Select(e => e.Id));
		var employeeStageMap = await employeeStageResolver.ResolveEmployeeStagesAsync(allEmployeeIds, scope);
		var scopedEmployeeIds = await FilterEmployeeIdsByScopeAsync(allEmployeeIds, employeeStageMap, scope);
		empQuery = empQuery.Where(e => scopedEmployeeIds.Contains(e.Id));

		var attBaseQuery = await employeeAttendanceRepository.GetQueryableAsync();

		var attForSummary = await asyncExecuter.ToListAsync(
			attBaseQuery.Where(a =>
				scopedEmployeeIds.Contains(a.EmployeeId) &&
				a.AttendanceDate == dateOnly));
		var approvedLeaves = await employeeLeaveRepository.GetApprovedAttendanceLeaveRowsForDateAsync(
			scopedEmployeeIds,
			dateOnly);
		var approvedLeaveByEmployee = approvedLeaves
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => g.OrderBy(x => x.StartDate).First());

		var employees = await asyncExecuter.ToListAsync(
			empQuery.Skip(input.SkipCount).Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var ids = employees.Select(e => e.Id).ToList();
		var attRows = attForSummary.Where(a => ids.Contains(a.EmployeeId)).ToList();
		var attByEmp = attRows.GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.First());
		var shiftIds = employees.Where(e => e.ShiftId.HasValue).Select(e => e.ShiftId!.Value).Distinct().ToList();
		var shifts = await LoadShiftLookupAsync(shiftIds);
		var captureModeByEmployee = await captureModeLookupResolver.ResolveForDateAsync(ids, dateOnly);

		var items = TakeAttendanceDayProjection.BuildRows(employees, attByEmp, approvedLeaveByEmployee, shifts, captureModeByEmployee, date, holiday, holidayReason);
		var summary = TakeAttendanceDayProjection.BuildSummary(attForSummary, approvedLeaveByEmployee, scopedEmployeeIds.Count);
		var distinctCaptureModes = items
			.Select(x => x.CaptureMode)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(2)
			.ToList();
		var dayCaptureMode = distinctCaptureModes.Count == 1 ? distinctCaptureModes[0] : "integrated";

		return new TakeAttendanceDayResponse
		{
			TotalCount = scopedEmployeeIds.Count,
			Date = dateOnly,
			IsDayLocked = locked != null,
			IsHoliday = holiday != null,
			CaptureMode = dayCaptureMode,
			HolidayName = holiday?.HolidayName,
			HolidayReason = holidayReason,
			LockedByUserId = locked?.LockedByUserId,
			LockedAt = locked?.LockedAt,
			Summary = summary,
			Items = items
		};
	}

	private Task<List<Guid>> FilterEmployeeIdsByScopeAsync(
		List<Guid> employeeIds,
		Dictionary<Guid, Guid?> employeeStageMap,
		UserScopeContext scope)
	{
		return hrScopeAccessService.FilterEmployeeIdsByStageAsync(
			employeeIds,
			employeeStageMap,
			scope,
			includeUnknownStageEmployees: HrUserScopeAccess.HasSingleConcreteMediumStage(scope));
	}

	private async Task<Dictionary<Guid, ShiftManagement>> LoadShiftLookupAsync(List<Guid> shiftIds)
	{
		if (shiftIds.Count == 0)
		{
			return new Dictionary<Guid, ShiftManagement>();
		}

		var query = await shiftRepository.GetQueryableAsync();
		var shifts = await asyncExecuter.ToListAsync(query.Where(x => shiftIds.Contains(x.Id)));
		return shifts.ToDictionary(x => x.Id, x => x);
	}

}
