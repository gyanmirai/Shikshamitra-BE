using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister.AttendanceRegisterAdjustmentMapper;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

[RemoteService(false)]
public class AttendanceRegisterAdjustmentWorkflow(
	IRepository<Employee, Guid> employeeRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	HrEmployeeScopeQueryService employeeScopeQueryService,
	HrUserScopeAccessService hrScopeAccessService,
	AttendanceRegisterReader registerReader,
	AttendanceRegisterSnapshotInvalidator snapshotInvalidator,
	HrPolicyResolver policyResolver,
	HrAuditWriter auditWriter)
	: ApplicationService, ITransientDependency
{
	private const int RemarksMaxLength = 512;

	public async Task<AttendanceRegisterAdjustmentResult> AdjustAsync(AttendanceRegisterAdjustRequest input)
	{
		if (input.AttendanceId.HasValue)
		{
			return await BulkAdjustAsync(new AttendanceRegisterBulkAdjustRequest
			{
				AttendanceIds = [input.AttendanceId.Value],
				StatusWire = input.StatusWire,
				InTime = input.InTime,
				OutTime = input.OutTime,
				Remarks = input.Remarks,
				Reason = input.Reason
			});
		}

		if (!input.EmployeeId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

		var status = ParseWireStatus(input.StatusWire);
		var timeIn = ParseOptionalTime(input.InTime);
		var timeOut = ParseOptionalTime(input.OutTime);
		var remarks = Truncate(input.Remarks, RemarksMaxLength);
		var attendanceDate = ParseAdjustmentDate(input.AttendanceDate);
		var scope = await registerReader.EnsureCanUseRegisterAsync();
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(attendanceQuery.Where(x =>
			x.EmployeeId == input.EmployeeId.Value &&
			x.AttendanceDate == attendanceDate));
		var results = new List<AttendanceRegisterAdjustmentRowResult>();
		var changedEmployeeMonths = new HashSet<(Guid EmployeeId, string MonthYear)>();

		if (existing != null)
		{
			var holidayLookup = await registerReader.BuildHolidayLookupAsync([existing.AttendanceDate]);
			await TryAdjustRowAsync(input.Reason, existing, status, timeIn, timeOut, remarks, holidayLookup, results, changedEmployeeMonths, scope);
		}
		else
		{
			await TryCreateRowAsync(input, input.EmployeeId.Value, attendanceDate, status, timeIn, timeOut, remarks, results, changedEmployeeMonths, scope);
		}

		await snapshotInvalidator.InvalidateUnlockedSnapshotsAsync(changedEmployeeMonths);
		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		return new AttendanceRegisterAdjustmentResult
		{
			UpdatedCount = results.Count(x => x.Updated),
			SkippedCount = results.Count(x => !x.Updated),
			Rows = results
		};
	}

	public async Task<AttendanceRegisterAdjustmentResult> BulkAdjustAsync(AttendanceRegisterBulkAdjustRequest input)
	{
		if (input.AttendanceIds.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceEmptyEntries);
		}

		var status = ParseWireStatus(input.StatusWire);
		var timeIn = ParseOptionalTime(input.InTime);
		var timeOut = ParseOptionalTime(input.OutTime);
		var remarks = Truncate(input.Remarks, RemarksMaxLength);
		var scope = await registerReader.EnsureCanUseRegisterAsync();
		var uniqueIds = input.AttendanceIds.Distinct().ToList();
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();

		var rows = await AsyncExecuter.ToListAsync(attendanceQuery.Where(x => uniqueIds.Contains(x.Id)));
		var foundById = rows.ToDictionary(x => x.Id);
		var holidayLookup = await registerReader.BuildHolidayLookupAsync(rows.Select(x => x.AttendanceDate));
		var results = new List<AttendanceRegisterAdjustmentRowResult>();
		var changedEmployeeMonths = new HashSet<(Guid EmployeeId, string MonthYear)>();

		foreach (var id in uniqueIds)
		{
			if (!foundById.TryGetValue(id, out var row))
			{
				results.Add(new AttendanceRegisterAdjustmentRowResult
				{
					AttendanceId = id,
					Updated = false,
					Reason = "Attendance row not found."
				});
				continue;
			}

			await TryAdjustRowAsync(input.Reason, row, status, timeIn, timeOut, remarks, holidayLookup, results, changedEmployeeMonths, scope);
		}

		await snapshotInvalidator.InvalidateUnlockedSnapshotsAsync(changedEmployeeMonths);
		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		return new AttendanceRegisterAdjustmentResult
		{
			UpdatedCount = results.Count(x => x.Updated),
			SkippedCount = results.Count(x => !x.Updated),
			Rows = results
		};
	}

	private async Task TryAdjustRowAsync(
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
			results.Add(ToSkipped(row, "Employee is outside your organization scope."));
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
		(timeIn, timeOut) = await NormalizeAdjustmentTimesAsync(row.EmployeeId, row.AttendanceDate, timeIn, timeOut);

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

	private async Task TryCreateRowAsync(
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
			results.Add(new AttendanceRegisterAdjustmentRowResult
			{
				AttendanceId = Guid.Empty,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
				Updated = false,
				Reason = "Employee is not active on this date."
			});
			return;
		}

		var stageLookup = await employeeScopeQueryService.BuildEmployeeStageLookupAsync([employeeId]);
		var stageIds = stageLookup.TryGetValue(employeeId, out var assignedStages) ? assignedStages : [];
		var resolvedStageId = employeeScopeQueryService.ResolveDisplayStage(stageIds, scope);

		if (!await hrScopeAccessService.CanAccessEmployeeStageAsync(scope, resolvedStageId))
		{
			results.Add(new AttendanceRegisterAdjustmentRowResult
			{
				AttendanceId = Guid.Empty,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
				Updated = false,
				Reason = "Employee is outside your organization scope."
			});
			return;
		}

		var holidayLookup = await registerReader.BuildHolidayLookupAsync([attendanceDate]);
		if (holidayLookup.TryGetValue(attendanceDate, out var holiday))
		{
			results.Add(new AttendanceRegisterAdjustmentRowResult
			{
				AttendanceId = Guid.Empty,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
				Updated = false,
				Reason = $"Holiday: {holiday.Name}"
			});
			return;
		}

		var dayLock = await staffAttendanceDayLockRepository.FindAsync(attendanceDate);
		if (dayLock != null)
		{
			results.Add(new AttendanceRegisterAdjustmentRowResult
			{
				AttendanceId = Guid.Empty,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
				Updated = false,
				Reason = "Attendance day is locked."
			});
			return;
		}

		var payrollLock = await registerReader.ResolvePayrollLockAsync(employeeId, ToMonthYear(attendanceDate));
		if (payrollLock.IsLocked)
		{
			results.Add(new AttendanceRegisterAdjustmentRowResult
			{
				AttendanceId = Guid.Empty,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate.ToString("yyyy-MM-dd"),
				Updated = false,
				Reason = payrollLock.Reason ?? "Payroll is locked."
			});
			return;
		}

		var (normalizedTimeIn, normalizedTimeOut) = await NormalizeAdjustmentTimesAsync(employeeId, attendanceDate, timeIn, timeOut);
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

	private async Task<(DateTimeOffset? TimeIn, DateTimeOffset? TimeOut)> NormalizeAdjustmentTimesAsync(
		Guid employeeId,
		DateOnly attendanceDate,
		DateTimeOffset? timeIn,
		DateTimeOffset? timeOut)
	{
		var policies = await policyResolver.ResolveAsync(
			employeeId,
			attendanceDate.ToDateTime(TimeOnly.MinValue));
		return policies.AttendancePolicy.CaptureMode == AttendanceCaptureMode.ManualStatus
			? (null, null)
			: (timeIn, timeOut);
	}
}
