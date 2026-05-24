using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View)]
public class AttendanceRegisterAppService(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EducationMediumStage, Guid> mediumStageRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	IRepository<SalarySlip, Guid> salarySlipRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	HrAuditWriter auditWriter)
	: ApplicationService, IAttendanceRegisterAppService
{
	private const int MaxDateRangeDays = 366;
	private const int RemarksMaxLength = 512;

	public async Task<AttendanceRegisterResponse> GetAsync(GetAttendanceRegisterInput input)
	{
		var (startDate, endDate) = ParseDateRange(input.StartDate, input.EndDate);
		var scope = await EnsureCanUseRegisterAsync();
		if (input.EducationMediumStageId.HasValue)
		{
			await EnsureStageInScopeAsync(scope, input.EducationMediumStageId);
		}

		var query = await BuildFilteredQueryAsync(input, startDate, endDate, scope);
		var totalBeforePayrollFilter = await AsyncExecuter.CountAsync(query);
		var allRowsForSummary = await AsyncExecuter.ToListAsync(query.Select(x => new RegisterProjection
		{
			AttendanceId = x.Attendance.Id,
			EmployeeId = x.Attendance.EmployeeId,
			AttendanceDate = x.Attendance.AttendanceDate,
			Status = x.Attendance.StatusType,
			EducationMediumStageId = x.Attendance.EducationMediumStageId,
			EmployeeName = ((x.Employee.FirstName ?? "") + " " + (x.Employee.LastName ?? "")).Trim(),
			EmployeeCode = x.Employee.EmployeeCode,
			Department = x.Employee.Department,
			EmployeeGroup = x.Employee.EmployeeGroup,
			RoleId = x.Employee.RoleId,
			TimeIn = x.Attendance.TimeIn,
			TimeOut = x.Attendance.TimeOut,
			Remarks = x.Attendance.Remarks
		}));

		var lockLookup = await BuildLockLookupAsync(allRowsForSummary);
		var filteredRows = ApplyPayrollStateFilter(allRowsForSummary, lockLookup, input.PayrollState);
		var totalCount = filteredRows.Count;
		var maxResultCount = NormalizeMaxResultCount(input.MaxResultCount);
		var skipCount = Math.Max(input.SkipCount, 0);
		var pageRows = filteredRows
			.OrderByDescending(x => x.AttendanceDate)
			.ThenBy(x => x.EmployeeName)
			.Skip(skipCount)
			.Take(maxResultCount)
			.ToList();
		var stageNames = await BuildStageNameLookupAsync(pageRows.Select(x => x.EducationMediumStageId).Where(x => x.HasValue).Select(x => x!.Value));
		var dayLocks = await BuildDayLockLookupAsync(pageRows.Select(x => x.AttendanceDate));

		return new AttendanceRegisterResponse
		{
			TotalCount = totalCount,
			CurrentPage = maxResultCount == 0 ? 1 : (skipCount / maxResultCount) + 1,
			PageSize = maxResultCount,
			TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / maxResultCount),
			Summary = BuildSummary(filteredRows, lockLookup),
			Items = pageRows.Select(x => MapRow(x, lockLookup, stageNames, dayLocks)).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.Add)]
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

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.Add)]
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
		var scope = await EnsureCanUseRegisterAsync();
		var uniqueIds = input.AttendanceIds.Distinct().ToList();
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var rows = await AsyncExecuter.ToListAsync(attendanceQuery.Where(x => uniqueIds.Contains(x.Id)));
		var foundById = rows.ToDictionary(x => x.Id);
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

			if (!await hrScopeAccessService.CanAccessEmployeeStageAsync(scope, row.EducationMediumStageId))
			{
				results.Add(ToSkipped(row, "Employee is outside your organization scope."));
				continue;
			}

			var dayLock = await staffAttendanceDayLockRepository.FindAsync(row.AttendanceDate);
			if (dayLock != null)
			{
				results.Add(ToSkipped(row, "Attendance day is locked."));
				continue;
			}

			var payrollLock = await ResolvePayrollLockAsync(row.EmployeeId, ToMonthYear(row.AttendanceDate));
			if (payrollLock.IsLocked)
			{
				results.Add(ToSkipped(row, payrollLock.Reason ?? "Payroll is locked."));
				continue;
			}

			var before = new
			{
				row.StatusType,
				row.TimeIn,
				row.TimeOut,
				row.Remarks
			};

			row.StatusType = status;
			row.TimeIn = timeIn;
			row.TimeOut = timeOut;
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

			results.Add(new AttendanceRegisterAdjustmentRowResult
			{
				AttendanceId = row.Id,
				EmployeeId = row.EmployeeId,
				AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
				Updated = true
			});
		}

		await InvalidateUnlockedSnapshotsAsync(changedEmployeeMonths);
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

	private async Task<IQueryable<RegisterQueryRow>> BuildFilteredQueryAsync(
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		UserScopeContext scope)
	{
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.TenantId == CurrentTenant.Id);
			employeeQuery = employeeQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		attendanceQuery = attendanceQuery.Where(x => x.AttendanceDate >= startDate && x.AttendanceDate <= endDate);
		if (input.EducationMediumStageId.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.EducationMediumStageId == input.EducationMediumStageId);
		}
		else if (!HrUserScopeAccess.HasHrWideAccess(scope))
		{
			attendanceQuery = attendanceQuery.Where(x => x.EducationMediumStageId.HasValue && scope.MediumStageIds.Contains(x.EducationMediumStageId.Value));
		}

		if (input.EmployeeId.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.EmployeeId == input.EmployeeId);
		}
		if (input.Status.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.StatusType == input.Status);
		}

		var query = from a in attendanceQuery
					join e in employeeQuery on a.EmployeeId equals e.Id
					select new RegisterQueryRow { Attendance = a, Employee = e };

		if (input.EmployeeGroup.HasValue)
		{
			query = query.Where(x => x.Employee.EmployeeGroup == input.EmployeeGroup);
		}
		if (input.IsTeaching.HasValue)
		{
			query = input.IsTeaching.Value
				? query.Where(x => x.Employee.EmployeeGroup == EmployeeGroupType.Teaching)
				: query.Where(x => x.Employee.EmployeeGroup != EmployeeGroupType.Teaching);
		}
		if (!string.IsNullOrWhiteSpace(input.Department))
		{
			var department = input.Department.Trim();
			query = query.Where(x => x.Employee.Department != null && x.Employee.Department.Contains(department));
		}
		if (input.RoleId.HasValue)
		{
			query = query.Where(x => x.Employee.RoleId == input.RoleId);
		}
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(x =>
				((x.Employee.FirstName ?? "") + " " + (x.Employee.MiddleName ?? "") + " " + (x.Employee.LastName ?? "")).ToLower().Contains(search) ||
				(x.Employee.EmployeeCode ?? "").ToLower().Contains(search) ||
				(x.Employee.MobileNo ?? "").ToLower().Contains(search) ||
				(x.Attendance.Remarks ?? "").ToLower().Contains(search));
		}

		return query;
	}

	private async Task<Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo>> BuildLockLookupAsync(List<RegisterProjection> rows)
	{
		var keys = rows.Select(x => (x.EmployeeId, MonthYear: ToMonthYear(x.AttendanceDate))).Distinct().ToList();
		var result = keys.ToDictionary(x => x, _ => new PayrollLockInfo(false, null));
		if (keys.Count == 0)
		{
			return result;
		}

		var employeeIds = keys.Select(x => x.EmployeeId).Distinct().ToList();
		var months = keys.Select(x => x.MonthYear).Distinct().ToList();
		var slipQuery = await salarySlipRepository.GetQueryableAsync();
		var snapshotQuery = await payrollRunItemSnapshotRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			slipQuery = slipQuery.Where(x => x.TenantId == CurrentTenant.Id);
			snapshotQuery = snapshotQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var finalizedSlips = await AsyncExecuter.ToListAsync(
			slipQuery.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				months.Contains(x.MonthYear) &&
				x.Status == SalarySlipStatus.Finalized)
			.Select(x => new { x.EmployeeId, x.MonthYear }));
		foreach (var slip in finalizedSlips)
		{
			result[(slip.EmployeeId, slip.MonthYear)] = new PayrollLockInfo(true, $"Payroll finalized for {slip.MonthYear}.");
		}

		var lockedSnapshots = await AsyncExecuter.ToListAsync(
			snapshotQuery.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				months.Contains(x.MonthYear) &&
				x.IsLocked)
			.Select(x => new { x.EmployeeId, x.MonthYear }));
		foreach (var snapshot in lockedSnapshots)
		{
			result[(snapshot.EmployeeId, snapshot.MonthYear)] = new PayrollLockInfo(true, $"Payroll locked for {snapshot.MonthYear}.");
		}

		return result;
	}

	private async Task<PayrollLockInfo> ResolvePayrollLockAsync(Guid employeeId, string monthYear)
	{
		var lookup = await BuildLockLookupAsync([new RegisterProjection { EmployeeId = employeeId, AttendanceDate = ParseMonthDate(monthYear) }]);
		return lookup.GetValueOrDefault((employeeId, monthYear), new PayrollLockInfo(false, null));
	}

	private async Task<Dictionary<Guid, string>> BuildStageNameLookupAsync(IEnumerable<Guid> stageIds)
	{
		var ids = stageIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, string>();
		}

		var query = await mediumStageRepository.GetQueryableAsync();
		return (await AsyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);
	}

	private async Task<HashSet<DateOnly>> BuildDayLockLookupAsync(IEnumerable<DateOnly> dates)
	{
		var result = new HashSet<DateOnly>();
		foreach (var date in dates.Distinct())
		{
			if (await staffAttendanceDayLockRepository.FindAsync(date) != null)
			{
				result.Add(date);
			}
		}
		return result;
	}

	private static List<RegisterProjection> ApplyPayrollStateFilter(
		List<RegisterProjection> rows,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		AttendanceRegisterPayrollState state)
	{
		return state switch
		{
			AttendanceRegisterPayrollState.Editable => rows.Where(x => !IsPayrollLocked(x, lockLookup)).ToList(),
			AttendanceRegisterPayrollState.Locked => rows.Where(x => IsPayrollLocked(x, lockLookup)).ToList(),
			_ => rows
		};
	}

	private static AttendanceRegisterSummaryDto BuildSummary(
		List<RegisterProjection> rows,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup)
	{
		return new AttendanceRegisterSummaryDto
		{
			Present = rows.Count(x => x.Status == EmployeeAttendanceStatusType.Present),
			HalfDay = rows.Count(x => x.Status == EmployeeAttendanceStatusType.HalfDayLeave),
			Absent = rows.Count(x => x.Status == EmployeeAttendanceStatusType.FullDayLeave),
			Locked = rows.Count(x => IsPayrollLocked(x, lockLookup)),
			Editable = rows.Count(x => !IsPayrollLocked(x, lockLookup))
		};
	}

	private static AttendanceRegisterRowDto MapRow(
		RegisterProjection row,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		Dictionary<Guid, string> stageNames,
		HashSet<DateOnly> dayLocks)
	{
		var payrollMonth = ToMonthYear(row.AttendanceDate);
		var lockInfo = lockLookup.GetValueOrDefault((row.EmployeeId, payrollMonth), new PayrollLockInfo(false, null));
		var isDayLocked = dayLocks.Contains(row.AttendanceDate);
		var lockReason = isDayLocked ? "Attendance day is locked." : lockInfo.Reason;
		return new AttendanceRegisterRowDto
		{
			AttendanceId = row.AttendanceId,
			EmployeeId = row.EmployeeId,
			EmployeeName = row.EmployeeName,
			EmployeeCode = row.EmployeeCode,
			Department = row.Department,
			EmployeeGroup = row.EmployeeGroup?.ToString(),
			EducationMediumStageId = row.EducationMediumStageId,
			EducationMediumStageName = row.EducationMediumStageId.HasValue
				? stageNames.GetValueOrDefault(row.EducationMediumStageId.Value)
				: null,
			RoleId = row.RoleId,
			AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			PayrollMonth = payrollMonth,
			InTime = row.TimeIn?.ToString("o"),
			OutTime = row.TimeOut?.ToString("o"),
			HoursWorked = CalculateHoursWorked(row.TimeIn, row.TimeOut),
			Remarks = row.Remarks,
			Status = row.Status,
			IsDayLocked = isDayLocked,
			IsPayrollLocked = lockInfo.IsLocked,
			CanEdit = !lockInfo.IsLocked && !isDayLocked,
			LockReason = lockReason
		};
	}

	private async Task InvalidateUnlockedSnapshotsAsync(HashSet<(Guid EmployeeId, string MonthYear)> keys)
	{
		if (keys.Count == 0)
		{
			return;
		}

		var employeeIds = keys.Select(x => x.EmployeeId).Distinct().ToList();
		var months = keys.Select(x => x.MonthYear).Distinct().ToList();
		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}

		var snapshots = await AsyncExecuter.ToListAsync(query.Where(x =>
			employeeIds.Contains(x.EmployeeId) &&
			months.Contains(x.MonthYear) &&
			!x.IsLocked));
		foreach (var snapshot in snapshots.Where(x => keys.Contains((x.EmployeeId, x.MonthYear))))
		{
			await payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: false);
		}
	}

	private async Task<UserScopeContext> EnsureCanUseRegisterAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope) || HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			return scope;
		}

		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	private async Task EnsureStageInScopeAsync(UserScopeContext scope, Guid? stageId)
	{
		if (await hrScopeAccessService.CanAccessEmployeeStageAsync(scope, stageId))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	private static AttendanceRegisterAdjustmentRowResult ToSkipped(EmployeeAttendance row, string reason) =>
		new()
		{
			AttendanceId = row.Id,
			EmployeeId = row.EmployeeId,
			AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			Updated = false,
			Reason = reason
		};

	private static (DateOnly Start, DateOnly End) ParseDateRange(string start, string end)
	{
		var startDate = DateOnly.FromDateTime(ParseDate(start));
		var endDate = DateOnly.FromDateTime(ParseDate(end));
		if (endDate < startDate)
		{
			throw new BusinessException(HRErrorCodes.AttendanceEndDateBeforeStartDate);
		}
		if (endDate.DayNumber - startDate.DayNumber > MaxDateRangeDays)
		{
			throw new BusinessException(HRErrorCodes.AttendanceDateRangeTooLarge);
		}
		return (startDate, endDate);
	}

	private static DateTime ParseDate(string dateString)
	{
		if (DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
		{
			return dto.Date;
		}
		if (DateTime.TryParseExact(dateString, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
		{
			return exact;
		}
		if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
		{
			return parsed;
		}
		throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	private static EmployeeAttendanceStatusType ParseWireStatus(string wire) =>
		(wire ?? "").Trim().ToUpperInvariant() switch
		{
			"PRESENT" => EmployeeAttendanceStatusType.Present,
			"HALF_DAY" or "HALFDAY" or "HALF_DAY_LEAVE" => EmployeeAttendanceStatusType.HalfDayLeave,
			"ABSENT" or "FULL_DAY_LEAVE" => EmployeeAttendanceStatusType.FullDayLeave,
			_ => throw new BusinessException(HRErrorCodes.TakeAttendanceInvalidStatusWire)
		};

	private static DateTimeOffset? ParseOptionalTime(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? null
			: DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
				? parsed
				: null;

	private static string? Truncate(string? value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.IsNullOrEmpty(value) ? value : value.Trim();
		}
		var trimmed = value.Trim();
		return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
	}

	private static string? CalculateHoursWorked(DateTimeOffset? timeIn, DateTimeOffset? timeOut)
	{
		if (!timeIn.HasValue || !timeOut.HasValue)
		{
			return null;
		}
		var duration = timeOut.Value - timeIn.Value;
		var hours = (int)duration.TotalHours;
		return $"{hours:D2}:{duration.Minutes:D2}";
	}

	private static string ToMonthYear(DateOnly date) => $"{date.Year:D4}-{date.Month:D2}";
	private static DateOnly ParseMonthDate(string monthYear) => new(int.Parse(monthYear[..4]), int.Parse(monthYear[5..7]), 1);
	private static bool IsPayrollLocked(RegisterProjection row, Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup) =>
		lockLookup.GetValueOrDefault((row.EmployeeId, ToMonthYear(row.AttendanceDate)), new PayrollLockInfo(false, null)).IsLocked;
	private static int NormalizeMaxResultCount(int maxResultCount) => maxResultCount is <= 0 or > 500 ? 50 : maxResultCount;

	private sealed class RegisterQueryRow
	{
		public EmployeeAttendance Attendance { get; set; } = default!;
		public Employee Employee { get; set; } = default!;
	}

	private sealed class RegisterProjection
	{
		public Guid AttendanceId { get; set; }
		public Guid EmployeeId { get; set; }
		public DateOnly AttendanceDate { get; set; }
		public EmployeeAttendanceStatusType Status { get; set; }
		public Guid? EducationMediumStageId { get; set; }
		public string EmployeeName { get; set; } = string.Empty;
		public string EmployeeCode { get; set; } = string.Empty;
		public string? Department { get; set; }
		public EmployeeGroupType? EmployeeGroup { get; set; }
		public Guid? RoleId { get; set; }
		public DateTimeOffset? TimeIn { get; set; }
		public DateTimeOffset? TimeOut { get; set; }
		public string? Remarks { get; set; }
	}

	private sealed record PayrollLockInfo(bool IsLocked, string? Reason);
}
