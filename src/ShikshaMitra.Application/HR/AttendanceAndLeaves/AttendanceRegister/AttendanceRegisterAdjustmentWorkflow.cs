using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister.AttendanceRegisterAdjustmentMapper;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

[RemoteService(false)]
public class AttendanceRegisterAdjustmentWorkflow(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	HrUserScopeAccessService hrScopeAccessService,
	AttendanceRegisterReader registerReader,
	AttendanceRegisterSnapshotInvalidator snapshotInvalidator,
	HrAuditWriter auditWriter)
	: ApplicationService, ITransientDependency
{
	private const int RemarksMaxLength = 512;

	public Task<AttendanceRegisterAdjustmentResult> AdjustAsync(AttendanceRegisterAdjustRequest input)
	{
		return BulkAdjustAsync(new AttendanceRegisterBulkAdjustRequest
		{
			AttendanceIds = [input.AttendanceId],
			StatusWire = input.StatusWire,
			InTime = input.InTime,
			OutTime = input.OutTime,
			Remarks = input.Remarks,
			Reason = input.Reason
		});
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

			await TryAdjustRowAsync(input, row, status, timeIn, timeOut, remarks, holidayLookup, results, changedEmployeeMonths, scope);
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
		AttendanceRegisterBulkAdjustRequest input,
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

		row.StatusType = status;
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
			reason: input.Reason,
			changedFields: HrAuditWriter.Diff(before, new { row.StatusType, row.TimeIn, row.TimeOut, row.Remarks }),
			context: new { row.EmployeeId, AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd"), PayrollMonth = ToMonthYear(row.AttendanceDate) });

		results.Add(ToUpdated(row));
	}
}
