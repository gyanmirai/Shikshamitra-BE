using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister.AttendanceRegisterAdjustmentMapper;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

public class AttendanceRegisterExistingRowAdjuster(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	HrUserScopeAccessService hrScopeAccessService,
	AttendanceRegisterReader registerReader,
	HrPolicyResolver policyResolver,
	HrAuditWriter auditWriter)
	: ITransientDependency
{
	internal async Task TryAdjustRowAsync(
		string? reason,
		EmployeeAttendance row,
		EmployeeAttendanceStatusType status,
		DateTimeOffset? timeIn,
		DateTimeOffset? timeOut,
		string? remarks,
		IReadOnlyDictionary<DateOnly, HolidayInfo> holidayLookup,
		List<AttendanceRegisterAdjustmentRowResult> results,
		HashSet<(Guid EmployeeId, string MonthYear)> changedEmployeeMonths,
		Security.UserScope.UserScopeContext scope)
	{
		if (!await hrScopeAccessService.CanAccessEmployeeStageAsync(scope, row.EducationMediumStageId))
		{
			results.Add(ToSkipped(row, "Staff is outside your organization scope."));
			return;
		}

		if (holidayLookup.TryGetValue(row.AttendanceDate, out var holiday))
		{
			results.Add(ToSkipped(row, $"Holiday: {holiday.Name}"));
			return;
		}

		var dayLock = await staffAttendanceDayLockRepository.FindAsync(row.AttendanceDate);
		if (dayLock != null)
		{
			results.Add(ToSkipped(row, "Attendance day is locked."));
			return;
		}

		var payrollLock = await registerReader.ResolvePayrollLockAsync(row.EmployeeId, ToMonthYear(row.AttendanceDate));
		if (payrollLock.IsLocked)
		{
			results.Add(ToSkipped(row, payrollLock.Reason ?? "Payroll is locked."));
			return;
		}

		var before = new
		{
			row.StatusType,
			row.TimeIn,
			row.TimeOut,
			row.Remarks
		};
		(timeIn, timeOut) = await AttendanceRegisterAdjustmentTimeNormalizer.NormalizeAsync(
			policyResolver,
			row.EmployeeId,
			row.AttendanceDate,
			timeIn,
			timeOut);

		row.StatusType = status;
		row.Source = AttendanceRecordSource.Correction;
		row.TimeIn = ApplyTimeToDate(row.AttendanceDate, timeIn);
		row.TimeOut = ApplyTimeToDate(row.AttendanceDate, timeOut);
		row.Remarks = remarks;
		await employeeAttendanceRepository.UpdateAsync(row, autoSave: false);
		changedEmployeeMonths.Add((row.EmployeeId, ToMonthYear(row.AttendanceDate)));

		await auditWriter.WriteAsync(
			academicYearId: null,
			entityType: "employee_attendance",
			entityId: row.Id,
			entityName: $"{row.EmployeeId}:{row.AttendanceDate:yyyy-MM-dd}",
			action: "adjusted",
			summary: "Attendance adjusted from register.",
			reason: reason,
			changedFields: HrAuditWriter.Diff(before, new { row.StatusType, row.TimeIn, row.TimeOut, row.Remarks }),
			context: new { row.EmployeeId, AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd"), PayrollMonth = ToMonthYear(row.AttendanceDate) });

		results.Add(ToUpdated(row));
	}
}
