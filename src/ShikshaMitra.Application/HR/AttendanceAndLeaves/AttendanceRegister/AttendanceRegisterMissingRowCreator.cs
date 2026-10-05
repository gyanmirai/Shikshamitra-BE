using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister.AttendanceRegisterAdjustmentMapper;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

public class AttendanceRegisterMissingRowCreator(
	IRepository<Employee, Guid> employeeRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	HrEmployeeScopeQueryService employeeScopeQueryService,
	HrUserScopeAccessService hrScopeAccessService,
	AttendanceRegisterReader registerReader,
	HrPolicyResolver policyResolver,
	HrAuditWriter auditWriter)
	: ApplicationService, ITransientDependency
{
	public async Task TryCreateRowAsync(
		AttendanceRegisterAdjustRequest input,
		Guid employeeId,
		DateOnly attendanceDate,
		EmployeeAttendanceStatusType status,
		DateTimeOffset? timeIn,
		DateTimeOffset? timeOut,
		string? remarks,
		List<AttendanceRegisterAdjustmentRowResult> results,
		HashSet<(Guid EmployeeId, string MonthYear)> changedEmployeeMonths,
		Security.UserScope.UserScopeContext scope)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		if (EmployeeLifecycleEligibility.GetEffectiveEmploymentStartDate(employee) > attendanceDate ||
		    !EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(
			    employee.EmployeeStatusType,
			    employee.StatusEffectiveDate,
			    employee.StatusLastChangedAt,
			    attendanceDate))
		{
			results.Add(ToSkipped(employeeId, attendanceDate, "Staff is not active on this date."));
			return;
		}

		var stageLookup = await employeeScopeQueryService.BuildEmployeeStageLookupAsync([employeeId]);
		var stageIds = stageLookup.TryGetValue(employeeId, out var assignedStages) ? assignedStages : [];
		var resolvedStageId = await employeeScopeQueryService.ResolveDisplayStageAsync(stageIds, scope);

		if (!await hrScopeAccessService.CanAccessEmployeeStageAsync(scope, resolvedStageId))
		{
			results.Add(ToSkipped(employeeId, attendanceDate, "Staff is outside your organization scope."));
			return;
		}

		var holidayLookup = await registerReader.BuildHolidayLookupAsync([attendanceDate]);
		if (holidayLookup.TryGetValue(attendanceDate, out var holiday))
		{
			results.Add(ToSkipped(employeeId, attendanceDate, $"Holiday: {holiday.Name}"));
			return;
		}

		var dayLock = await staffAttendanceDayLockRepository.FindAsync(attendanceDate);
		if (dayLock != null)
		{
			results.Add(ToSkipped(employeeId, attendanceDate, "Attendance day is locked."));
			return;
		}

		var payrollLock = await registerReader.ResolvePayrollLockAsync(employeeId, ToMonthYear(attendanceDate));
		if (payrollLock.IsLocked)
		{
			results.Add(ToSkipped(employeeId, attendanceDate, payrollLock.Reason ?? "Payroll is locked."));
			return;
		}

		var (normalizedTimeIn, normalizedTimeOut) = await AttendanceRegisterAdjustmentTimeNormalizer.NormalizeAsync(
			policyResolver,
			employeeId,
			attendanceDate,
			timeIn,
			timeOut);
		var row = new EmployeeAttendance
		{
			TenantId = CurrentTenant.Id,
			EmployeeId = employeeId,
			AttendanceDate = attendanceDate,
			EducationMediumStageId = resolvedStageId,
			StatusType = status,
			Source = AttendanceRecordSource.Correction,
			TimeIn = ApplyTimeToDate(attendanceDate, normalizedTimeIn),
			TimeOut = ApplyTimeToDate(attendanceDate, normalizedTimeOut),
			Remarks = remarks
		};

		await employeeAttendanceRepository.InsertAsync(row, autoSave: false);
		changedEmployeeMonths.Add((row.EmployeeId, ToMonthYear(row.AttendanceDate)));

		await auditWriter.WriteAsync(
			academicYearId: null,
			entityType: "employee_attendance",
			entityId: row.Id,
			entityName: $"{row.EmployeeId}:{row.AttendanceDate:yyyy-MM-dd}",
			action: "created",
			summary: "Attendance created from register.",
			reason: input.Reason,
			changedFields: HrAuditWriter.Diff(
				new { StatusType = (EmployeeAttendanceStatusType?)null, TimeIn = (DateTimeOffset?)null, TimeOut = (DateTimeOffset?)null, Remarks = (string?)null },
				new { StatusType = (EmployeeAttendanceStatusType?)row.StatusType, row.TimeIn, row.TimeOut, row.Remarks }),
			context: new { row.EmployeeId, AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd"), PayrollMonth = ToMonthYear(row.AttendanceDate) });

		results.Add(ToUpdated(row));
	}

	private static AttendanceRegisterAdjustmentRowResult ToSkipped(Guid employeeId, DateOnly attendanceDate, string reason) => new()
	{
		AttendanceId = Guid.Empty,
		EmployeeId = employeeId,
		AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
		Updated = false,
		Reason = reason
	};
}
