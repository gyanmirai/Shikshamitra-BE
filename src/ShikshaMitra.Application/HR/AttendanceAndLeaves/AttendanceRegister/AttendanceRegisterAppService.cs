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
	IRepository<EducationMediumStage, Guid> mediumStageRepository,
	IStaffAttendanceDayLockRepository staffAttendanceDayLockRepository,
	IRepository<SalarySlip, Guid> salarySlipRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IRepository<PayrollRunReview, Guid> payrollRunReviewRepository,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	HrEmployeeScopeQueryService hrEmployeeScopeQueryService,
	HrAuditWriter auditWriter)
	: ApplicationService, IAttendanceRegisterAppService
{
	private const int MaxDateRangeDays = 366;
	private const int RemarksMaxLength = 512;

	public async Task<AttendanceRegisterResponse> GetAsync(GetAttendanceRegisterInput input)
	{
		var (filteredRows, lockLookup) = await ResolveRegisterRowsAsync(input);
		var allDayLocks = await BuildDayLockLookupAsync(filteredRows.Select(x => x.AttendanceDate));
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
			Summary = BuildSummary(filteredRows, lockLookup, allDayLocks, filteredRows.Select(x => x.EmployeeId).Distinct().Count()),
			Items = pageRows.Select(x => MapRow(x, lockLookup, stageNames, dayLocks)).ToList()
		};
	}

	public async Task<GroupedAttendanceRegisterResponse> GetGroupedAsync(GetGroupedAttendanceRegisterInput input)
	{
		var (filteredRows, lockLookup) = await ResolveRegisterRowsAsync(input);
		var allDayLocks = await BuildDayLockLookupAsync(filteredRows.Select(x => x.AttendanceDate));
		var groupedRows = filteredRows
			.GroupBy(x => x.EmployeeId)
			.Select(group => BuildGroupedEmployee(group.ToList(), lockLookup, allDayLocks))
			.Where(group => MatchesExceptionFilter(group, input.ExceptionFilter))
			.OrderByDescending(group => group.HasExceptions)
			.ThenByDescending(group => group.Absent)
			.ThenByDescending(group => group.MissingPunch)
			.ThenByDescending(group => group.HalfDay)
			.ThenByDescending(group => group.Locked)
			.ThenBy(group => group.EmployeeName)
			.ToList();

		var totalCount = groupedRows.Count;
		var maxResultCount = NormalizeMaxResultCount(input.MaxResultCount);
		var skipCount = Math.Max(input.SkipCount, 0);
		var pageGroups = groupedRows.Skip(skipCount).Take(maxResultCount).ToList();
		var stageNames = await BuildStageNameLookupAsync(pageGroups.Select(x => x.EducationMediumStageId).Where(x => x.HasValue).Select(x => x!.Value));

		return new GroupedAttendanceRegisterResponse
		{
			TotalCount = totalCount,
			CurrentPage = maxResultCount == 0 ? 1 : (skipCount / maxResultCount) + 1,
			PageSize = maxResultCount,
			TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / maxResultCount),
			Summary = BuildSummary(groupedRows.SelectMany(x => x.RawDays).ToList(), lockLookup, allDayLocks, groupedRows.Count),
			Items = pageGroups.Select(x => MapGroupedEmployee(x, lockLookup, stageNames, allDayLocks)).ToList()
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

	private async Task<List<EmployeeProjection>> BuildFilteredEmployeesAsync(
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		UserScopeContext scope)
	{
		var employeeQuery = await hrEmployeeScopeQueryService.BuildScopedEmployeeQueryAsync(
			scope,
			new HrEmployeeScopeOptions
			{
				EducationMediumStageId = input.EducationMediumStageId,
				ActiveOnOrAfter = startDate,
				JoinedOnOrBefore = endDate,
				TreatNullStatusAsActive = true
			});

		if (input.EmployeeId.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.Id == input.EmployeeId);
		}
		if (input.EmployeeGroup.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.EmployeeGroup == input.EmployeeGroup);
		}
		if (input.IsTeaching.HasValue)
		{
			employeeQuery = input.IsTeaching.Value
				? employeeQuery.Where(x => x.EmployeeGroup == EmployeeGroupType.Teaching)
				: employeeQuery.Where(x => x.EmployeeGroup != EmployeeGroupType.Teaching);
		}
		if (!string.IsNullOrWhiteSpace(input.Department))
		{
			var department = input.Department.Trim();
			employeeQuery = employeeQuery.Where(x => x.Department != null && x.Department.Contains(department));
		}
		if (input.RoleId.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.RoleId == input.RoleId);
		}

		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Select(x => new EmployeeProjection
		{
			EmployeeId = x.Id,
			EmployeeName = ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim(),
			EmployeeCode = x.EmployeeCode,
			MobileNo = x.MobileNo,
			Department = x.Department,
			EmployeeGroup = x.EmployeeGroup,
			RoleId = x.RoleId,
			DateOfJoining = x.DateOfJoining,
			CreationTime = x.CreationTime,
			EmployeeStatusType = x.EmployeeStatusType,
			StatusLastChangedAt = x.StatusLastChangedAt,
			StatusEffectiveDate = x.StatusEffectiveDate
		}));

		if (employees.Count == 0)
		{
			return employees;
		}

		var employeeIds = employees.Select(x => x.EmployeeId).ToList();
		var stageLookup = await hrEmployeeScopeQueryService.BuildEmployeeStageLookupAsync(employeeIds);

		foreach (var employee in employees)
		{
			var stages = stageLookup.TryGetValue(employee.EmployeeId, out var assignedStages)
				? assignedStages
				: [];
			employee.EducationMediumStageId = hrEmployeeScopeQueryService.ResolveDisplayStage(stages, scope, input.EducationMediumStageId);
		}

		return employees;
	}

	private async Task<(List<RegisterProjection> Rows, Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> LockLookup)> ResolveRegisterRowsAsync(GetAttendanceRegisterInput input)
	{
		var (startDate, endDate) = ParseDateRange(input.StartDate, input.EndDate);
		var scope = await EnsureCanUseRegisterAsync();
		if (input.EducationMediumStageId.HasValue)
		{
			await EnsureStageInScopeAsync(scope, input.EducationMediumStageId);
		}

		var employees = await BuildFilteredEmployeesAsync(input, startDate, endDate, scope);
		var allRowsForSummary = await BuildEmployeeDrivenRowsAsync(input, startDate, endDate, employees, scope);

		var lockLookup = await BuildLockLookupAsync(allRowsForSummary);
		return (ApplyPayrollStateFilter(allRowsForSummary, lockLookup, input.PayrollState), lockLookup);
	}

	private async Task<List<RegisterProjection>> BuildEmployeeDrivenRowsAsync(
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		List<EmployeeProjection> employees,
		UserScopeContext scope)
	{
		if (employees.Count == 0)
		{
			return [];
		}

		var employeeIds = employees.Select(x => x.EmployeeId).ToList();
		var attendanceQuery = await hrEmployeeScopeQueryService.BuildScopedAttendanceQueryAsync(scope, input.EducationMediumStageId);
		attendanceQuery = attendanceQuery.Where(x =>
			employeeIds.Contains(x.EmployeeId) &&
			x.AttendanceDate >= startDate &&
			x.AttendanceDate <= endDate);

		var attendanceRows = await AsyncExecuter.ToListAsync(attendanceQuery);
		var attendanceByEmployeeDate = attendanceRows
			.GroupBy(x => (x.EmployeeId, x.AttendanceDate))
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastModificationTime ?? y.CreationTime).First());

		var rows = new List<RegisterProjection>();
		foreach (var employee in employees)
		{
			for (var date = startDate; date <= endDate; date = date.AddDays(1))
			{
				if (!IsEmployeeEligibleOnDate(employee, date))
				{
					continue;
				}

				attendanceByEmployeeDate.TryGetValue((employee.EmployeeId, date), out var attendance);
				if (input.Status.HasValue && attendance?.StatusType != input.Status.Value)
				{
					continue;
				}

				rows.Add(new RegisterProjection
				{
					AttendanceId = attendance?.Id,
					EmployeeId = employee.EmployeeId,
					AttendanceDate = date,
					Status = attendance == null
						? AttendanceRegisterStatus.NotMarked
						: MapRegisterStatus(attendance.StatusType),
					IsMarked = attendance != null,
					EducationMediumStageId = attendance?.EducationMediumStageId ?? employee.EducationMediumStageId,
					EmployeeName = employee.EmployeeName,
					EmployeeCode = employee.EmployeeCode,
					MobileNo = employee.MobileNo,
					Department = employee.Department,
					EmployeeGroup = employee.EmployeeGroup,
					RoleId = employee.RoleId,
					TimeIn = attendance?.TimeIn,
					TimeOut = attendance?.TimeOut,
					Remarks = attendance?.Remarks
				});
			}
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			rows = rows.Where(x => MatchesSearch(x, search)).ToList();
		}

		return rows;
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
			AttendanceRegisterPayrollState.Editable => rows.Where(x => x.IsMarked && !IsPayrollLocked(x, lockLookup)).ToList(),
			AttendanceRegisterPayrollState.Locked => rows.Where(x => IsPayrollLocked(x, lockLookup)).ToList(),
			_ => rows
		};
	}

	private static AttendanceRegisterSummaryDto BuildSummary(
		List<RegisterProjection> rows,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		HashSet<DateOnly> dayLocks,
		int staffCount)
	{
		return new AttendanceRegisterSummaryDto
		{
			StaffCount = staffCount,
			Present = rows.Count(x => x.Status == AttendanceRegisterStatus.Present),
			HalfDay = rows.Count(x => x.Status == AttendanceRegisterStatus.HalfDay),
			Absent = rows.Count(x => x.Status == AttendanceRegisterStatus.Absent),
			NotMarked = rows.Count(x => x.Status == AttendanceRegisterStatus.NotMarked),
			Locked = rows.Count(x => IsLocked(x, lockLookup, dayLocks)),
			Editable = rows.Count(x => x.IsMarked && !IsLocked(x, lockLookup, dayLocks)),
			MissingPunch = rows.Count(IsMissingPunch)
		};
	}

	private static GroupedRegisterProjection BuildGroupedEmployee(
		List<RegisterProjection> rows,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		HashSet<DateOnly> dayLocks)
	{
		var first = rows.OrderBy(x => x.AttendanceDate).First();
		var orderedRows = rows.OrderBy(x => x.AttendanceDate).ToList();
		var absent = orderedRows.Count(x => x.Status == AttendanceRegisterStatus.Absent);
		var halfDay = orderedRows.Count(x => x.Status == AttendanceRegisterStatus.HalfDay);
		var missingPunch = orderedRows.Count(IsMissingPunch);
		var locked = orderedRows.Count(x => IsLocked(x, lockLookup, dayLocks));
		return new GroupedRegisterProjection
		{
			EmployeeId = first.EmployeeId,
			EmployeeName = first.EmployeeName,
			EmployeeCode = first.EmployeeCode,
			Department = first.Department,
			EmployeeGroup = first.EmployeeGroup,
			EducationMediumStageId = first.EducationMediumStageId,
			RoleId = first.RoleId,
			RecordedDays = orderedRows.Count,
			Present = orderedRows.Count(x => x.Status == AttendanceRegisterStatus.Present),
			HalfDay = halfDay,
			Absent = absent,
			Editable = orderedRows.Count(x => x.IsMarked && !IsLocked(x, lockLookup, dayLocks)),
			Locked = locked,
			MissingPunch = missingPunch,
			HasExceptions = absent > 0 || halfDay > 0 || missingPunch > 0 || locked > 0,
			RawDays = orderedRows
		};
	}

	private static bool MatchesExceptionFilter(GroupedRegisterProjection group, AttendanceRegisterExceptionFilter filter) =>
		filter switch
		{
			AttendanceRegisterExceptionFilter.NeedsAttention => group.HasExceptions,
			AttendanceRegisterExceptionFilter.Absent => group.Absent > 0,
			AttendanceRegisterExceptionFilter.HalfDay => group.HalfDay > 0,
			AttendanceRegisterExceptionFilter.MissingPunch => group.MissingPunch > 0,
			AttendanceRegisterExceptionFilter.Locked => group.Locked > 0,
			_ => true
		};

	private static GroupedAttendanceRegisterEmployeeDto MapGroupedEmployee(
		GroupedRegisterProjection group,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		Dictionary<Guid, string> stageNames,
		HashSet<DateOnly> dayLocks) =>
		new()
		{
			EmployeeId = group.EmployeeId,
			EmployeeName = group.EmployeeName,
			EmployeeCode = group.EmployeeCode,
			Department = group.Department,
			EmployeeGroup = group.EmployeeGroup?.ToString(),
			EducationMediumStageId = group.EducationMediumStageId,
			EducationMediumStageName = group.EducationMediumStageId.HasValue
				? stageNames.GetValueOrDefault(group.EducationMediumStageId.Value)
				: null,
			RoleId = group.RoleId,
			RecordedDays = group.RecordedDays,
			Present = group.Present,
			HalfDay = group.HalfDay,
			Absent = group.Absent,
			Editable = group.Editable,
			Locked = group.Locked,
			MissingPunch = group.MissingPunch,
			HasExceptions = group.HasExceptions,
			Days = group.RawDays.Select(x => MapRow(x, lockLookup, stageNames, dayLocks)).ToList()
		};

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
			IsMarked = row.IsMarked,
			IsDayLocked = isDayLocked,
			IsPayrollLocked = lockInfo.IsLocked,
			CanEdit = row.IsMarked && !lockInfo.IsLocked && !isDayLocked,
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
		else
		{
			query = query.Where(x => x.TenantId == null);
		}

		var snapshots = await AsyncExecuter.ToListAsync(query.Where(x =>
			employeeIds.Contains(x.EmployeeId) &&
			months.Contains(x.MonthYear) &&
			!x.IsLocked));
		foreach (var snapshot in snapshots.Where(x => keys.Contains((x.EmployeeId, x.MonthYear))))
		{
			await payrollRunItemSnapshotRepository.DeleteAsync(snapshot, autoSave: false);
		}

		var reviewQuery = await payrollRunReviewRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			reviewQuery = reviewQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			reviewQuery = reviewQuery.Where(x => x.TenantId == null);
		}

		var reviews = await AsyncExecuter.ToListAsync(reviewQuery.Where(x =>
			employeeIds.Contains(x.EmployeeId) &&
			months.Contains(x.MonthYear)));
		foreach (var review in reviews.Where(x => keys.Contains((x.EmployeeId, x.MonthYear))))
		{
			await payrollRunReviewRepository.DeleteAsync(review, autoSave: false);
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

	private static DateTimeOffset? ApplyTimeToDate(DateOnly attendanceDate, DateTimeOffset? time)
	{
		if (!time.HasValue)
		{
			return null;
		}

		var dateTime = attendanceDate.ToDateTime(TimeOnly.FromDateTime(time.Value.DateTime));
		return new DateTimeOffset(dateTime, time.Value.Offset);
	}

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
	private static AttendanceRegisterStatus MapRegisterStatus(EmployeeAttendanceStatusType status) =>
		status switch
		{
			EmployeeAttendanceStatusType.Present => AttendanceRegisterStatus.Present,
			EmployeeAttendanceStatusType.HalfDayLeave => AttendanceRegisterStatus.HalfDay,
			EmployeeAttendanceStatusType.FullDayLeave => AttendanceRegisterStatus.Absent,
			_ => AttendanceRegisterStatus.NotMarked
		};

	private static bool IsEmployeeEligibleOnDate(EmployeeProjection employee, DateOnly date)
	{
		var startDate = employee.DateOfJoining ?? DateOnly.FromDateTime(employee.CreationTime.Date);
		if (startDate > date)
		{
			return false;
		}

		return EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(
			employee.EmployeeStatusType,
			employee.StatusEffectiveDate,
			date);
	}

	private static bool IsPayrollLocked(RegisterProjection row, Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup) =>
		lockLookup.GetValueOrDefault((row.EmployeeId, ToMonthYear(row.AttendanceDate)), new PayrollLockInfo(false, null)).IsLocked;
	private static bool IsLocked(RegisterProjection row, Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup, HashSet<DateOnly> dayLocks) =>
		dayLocks.Contains(row.AttendanceDate) || IsPayrollLocked(row, lockLookup);
	private static bool IsMissingPunch(RegisterProjection row) =>
		row.Status == AttendanceRegisterStatus.Present && (!row.TimeIn.HasValue || !row.TimeOut.HasValue);
	private static bool MatchesSearch(RegisterProjection row, string search) =>
		Contains(row.EmployeeName, search) ||
		Contains(row.EmployeeCode, search) ||
		Contains(row.MobileNo, search) ||
		Contains(row.Remarks, search);
	private static bool Contains(string? value, string search) =>
		!string.IsNullOrWhiteSpace(value) &&
		value.Contains(search, StringComparison.OrdinalIgnoreCase);
	private static int NormalizeMaxResultCount(int maxResultCount) => maxResultCount is <= 0 or > 500 ? 50 : maxResultCount;

	private sealed class EmployeeProjection
	{
		public Guid EmployeeId { get; set; }
		public string EmployeeName { get; set; } = string.Empty;
		public string EmployeeCode { get; set; } = string.Empty;
		public string? MobileNo { get; set; }
		public string? Department { get; set; }
		public EmployeeGroupType? EmployeeGroup { get; set; }
		public Guid? EducationMediumStageId { get; set; }
		public Guid? RoleId { get; set; }
		public DateOnly? DateOfJoining { get; set; }
		public DateTime CreationTime { get; set; }
		public EmployeeStatusType? EmployeeStatusType { get; set; }
		public DateTimeOffset? StatusLastChangedAt { get; set; }
		public DateOnly? StatusEffectiveDate { get; set; }
	}

	private sealed class RegisterProjection
	{
		public Guid? AttendanceId { get; set; }
		public Guid EmployeeId { get; set; }
		public DateOnly AttendanceDate { get; set; }
		public AttendanceRegisterStatus Status { get; set; }
		public bool IsMarked { get; set; }
		public Guid? EducationMediumStageId { get; set; }
		public string EmployeeName { get; set; } = string.Empty;
		public string EmployeeCode { get; set; } = string.Empty;
		public string? MobileNo { get; set; }
		public string? Department { get; set; }
		public EmployeeGroupType? EmployeeGroup { get; set; }
		public Guid? RoleId { get; set; }
		public DateTimeOffset? TimeIn { get; set; }
		public DateTimeOffset? TimeOut { get; set; }
		public string? Remarks { get; set; }
	}

	private sealed class GroupedRegisterProjection
	{
		public Guid EmployeeId { get; set; }
		public string EmployeeName { get; set; } = string.Empty;
		public string EmployeeCode { get; set; } = string.Empty;
		public string? Department { get; set; }
		public EmployeeGroupType? EmployeeGroup { get; set; }
		public Guid? EducationMediumStageId { get; set; }
		public Guid? RoleId { get; set; }
		public int RecordedDays { get; set; }
		public int Present { get; set; }
		public int HalfDay { get; set; }
		public int Absent { get; set; }
		public int Editable { get; set; }
		public int Locked { get; set; }
		public int MissingPunch { get; set; }
		public bool HasExceptions { get; set; }
		public List<RegisterProjection> RawDays { get; set; } = [];
	}

	private sealed record PayrollLockInfo(bool IsLocked, string? Reason);
}
