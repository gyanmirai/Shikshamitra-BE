using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterLockState;

namespace ShikshaMitra.HR.Reports;

public record HrReportingDatasetRows(List<Dictionary<string, object?>> Rows, List<string> Warnings);

public class HrReportingDataReader(
	HrEmployeeScopeQueryService employeeScope, HrReportRowSourceReader sourceReader,
	AttendanceRegisterRowReader attendanceReader, LeaveActorAccessService leaveAccess,
	LeaveBalanceReader balances, LeaveWorkCalendarUnitService leaveUnits,
	IRepository<EmployeeLeave, Guid> leaveRepository, ProfessionalManagementReadScopeService reviewScope,
	IRepository<PerformanceReviewAssignment, Guid> assignments, IRepository<ProfessionalReviewCycle, Guid> cycles, IRepository<ShiftManagement, Guid> shifts, IAsyncQueryableExecuter executor)
	: ITransientDependency
{
	internal async Task<IQueryable<Employee>> EmployeesAsync(string dataset, HrReportParametersDto p, UserScopeContext scope)
	{
		var employees = await employeeScope.BuildScopedEmployeeQueryAsync(scope, new HrEmployeeScopeOptions
		{
			AcademicYearId = p.AcademicYearId,
			EducationMediumStageId = p.EducationMediumStageId,
			ActiveOnOrAfter = dataset == "attendance" ? p.FromDate : null,
			JoinedOnOrBefore = dataset == "attendance" ? p.ToDate : null,
			TreatNullStatusAsActive = true,
			IncludeUnassignedEmployeesWhenAcademicYearScoped = dataset == "attendance" && p.AcademicYearId.HasValue && !p.EducationMediumStageId.HasValue
		});
		if (p.EmployeeIds.Count > 0) { var ids = p.EmployeeIds.ToArray(); employees = employees.Where(x => Enumerable.Contains(ids, x.Id)); }
		if (p.Departments.Count > 0) { var departments = p.Departments.ToArray(); employees = employees.Where(x => x.Department != null && Enumerable.Contains(departments, x.Department)); }
		if (p.EmployeeGroup.HasValue) { var group = (EmployeeGroupType) p.EmployeeGroup.Value; employees = employees.Where(x => x.EmployeeGroup == group); }
		if (dataset is "leave" or "leave_balances")
		{
			var allowed = await leaveAccess.ResolveScopedEmployeeIdsAsync(await leaveAccess.ResolveContextAsync());
			if (allowed != null) { var ids = allowed.ToArray(); employees = employees.Where(x => Enumerable.Contains(ids, x.Id)); }
		}
		if (dataset == "performance")
		{
			var reviews = await reviewScope.ApplyAsync(await assignments.GetQueryableAsync());
			if (p.CycleId.HasValue) reviews = reviews.Where(x => x.CycleId == p.CycleId.Value);
			employees = employees.Where(x => reviews.Select(r => r.EmployeeId).Contains(x.Id));
		}
		return employees;
	}
	public async Task<HrReportingDatasetRows> ReadAsync(HrReportDesignDto d, HrReportParametersDto p, UserScopeContext scope, DateTimeOffset now, string timezone, Func<Task>? ensureActive = null)
	{
		var warnings = new List<string>();
		var rows = await ReadRowsAsync(d, p, scope, now, timezone, warnings, ensureActive);
		return new(rows, warnings.Distinct().ToList());
	}
	private async Task<List<Dictionary<string, object?>>> ReadRowsAsync(HrReportDesignDto d, HrReportParametersDto p, UserScopeContext scope, DateTimeOffset now, string timezone, List<string> warnings, Func<Task>? ensureActive)
	{
		var employees = await EmployeesAsync(d.DataSource, p, scope);
		if (await executor.CountAsync(employees) > HrReportingEngine.MaximumPopulation) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
		var staff = await executor.ToListAsync(employees.Select(x => new { x.Id, x.EmployeeCode, x.FirstName, x.LastName, x.Department, x.Designation, x.EmployeeStatusType, x.StatusEffectiveDate, x.DateOfJoining, x.MobileNo, x.Email, x.DateOfBirth, x.GenderType, x.ShiftId }));
		if (staff.Count == 0) return [];
		var staffIds = staff.Select(x => x.Id).ToArray();
		var staffById = staff.ToDictionary(x => x.Id);
		if (d.DataSource == "performance")
		{
			var query = await reviewScope.ApplyAsync(await assignments.GetQueryableAsync());
			query = query.Where(x => Enumerable.Contains(staffIds, x.EmployeeId));
			if (p.CycleId.HasValue) query = query.Where(x => x.CycleId == p.CycleId.Value);
			if (await executor.CountAsync(query) > HrReportingEngine.MaximumPopulation) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
			var cycleNames = (await executor.ToListAsync(await cycles.GetQueryableAsync())).ToDictionary(x => x.Id, x => x.Name);
			return (await executor.ToListAsync(query.OrderBy(x => x.CycleId).ThenBy(x => x.Id))).Select(x =>
			{
				var e = staffById[x.EmployeeId];
				return new Dictionary<string, object?>
				{
					["__employeeId"] = e.Id,
					["__assignmentId"] = x.Id,
					["cycle"] = cycleNames.GetValueOrDefault(x.CycleId),
					["employeeCode"] = e.EmployeeCode,
					["employeeName"] = (e.FirstName + " " + e.LastName).Trim(),
					["department"] = e.Department,
					["status"] = x.Status.ToString(),
					["finalScore"] = x.FinalScore,
					["finalRating"] = x.FinalRating,
					["finalizedAt"] = x.FinalizedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(x.FinalizedAt.Value, DateTimeKind.Utc)) : null
				};
			}).ToList();
		}
		if (d.DataSource == "employee")
		{
			var rows = staff.Select(e => new Dictionary<string, object?>
			{
				["__employeeId"] = e.Id,
				["employeeCode"] = e.EmployeeCode,
				["employeeName"] = (e.FirstName + " " + e.LastName).Trim(),
				["department"] = e.Department,
				["designation"] = e.Designation,
				["status"] = e.EmployeeStatusType?.ToString(),
				["statusEffectiveDate"] = e.StatusEffectiveDate,
				["dateOfJoining"] = e.DateOfJoining,
				["leavingDate"] = e.EmployeeStatusType is EmployeeStatusType.Left or EmployeeStatusType.Retired ? e.StatusEffectiveDate : null,
				["lifecycleDate"] = e.EmployeeStatusType is EmployeeStatusType.Left or EmployeeStatusType.Retired ? e.StatusEffectiveDate ?? e.DateOfJoining : e.DateOfJoining,
				["mobileNo"] = e.MobileNo,
				["email"] = e.Email,
				["dateOfBirth"] = e.DateOfBirth,
				["gender"] = e.GenderType.ToString()
			}).ToList();
			if (!string.IsNullOrEmpty(d.DateField) && p.FromDate.HasValue)
				rows = rows.Where(x => d.DateField == "lifecycleDate" ? InRange(x.GetValueOrDefault("dateOfJoining"), p) || InRange(x.GetValueOrDefault("leavingDate"), p) : InRange(x.GetValueOrDefault(d.DateField), p)).ToList();
			return rows;
		}
		if (d.DataSource == "leave_balances")
		{
			var result = new List<Dictionary<string, object?>>();
			var currentBalances = await balances.GetBalancesForAuthorizedEmployeesAsync(p.AcademicYearId!.Value, await executor.ToListAsync(employees));
			foreach (var e in staff)
			{
				var balance = currentBalances[e.Id];
				result.AddRange(balance.Items.Select(b => new Dictionary<string, object?>
				{
					["__employeeId"] = e.Id,
					["employeeCode"] = e.EmployeeCode,
					["employeeName"] = (e.FirstName + " " + e.LastName).Trim(),
					["policy"] = b.PolicyName,
					["leaveType"] = b.PaidType,
					["accrued"] = b.AccruedDays,
					["carryForward"] = b.CarryForwardDays,
					["used"] = b.UsedDays,
					["pending"] = b.PendingDays,
					["remaining"] = b.RemainingDays,
					["unlimited"] = b.IsUnlimited
				}));
			}
			return result;
		}
		if (d.DataSource is "attendance" or "leave")
		{
			var from = p.FromDate ?? throw HrReportingEngine.Invalid("Choose a date range."); var to = p.ToDate!.Value;
			var leaveQuery = (await leaveRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(staffIds, x.EmployeeId) && x.EndDateOnly >= from && x.StartDateOnly <= to);
			if (await executor.CountAsync(leaveQuery) > HrReportingEngine.MaximumPopulation) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
			var leaves = await executor.ToListAsync(leaveQuery);
			if (d.DataSource == "leave")
			{
				var result = new List<Dictionary<string, object?>>();
				foreach (var leave in leaves)
				{
					var e = staffById[leave.EmployeeId];
					var units = leave.ApprovedLeaveUnits;
					if (leave.Status == EmployeeLeaveStatusType.Approved && (leave.StartDateOnly < from || leave.EndDateOnly > to))
						units = Math.Min(units, await leaveUnits.CalculateUnitsAsync(e.Id, (leave.StartDateOnly < from ? from : leave.StartDateOnly).ToDateTime(TimeOnly.MinValue), (leave.EndDateOnly > to ? to : leave.EndDateOnly).ToDateTime(TimeOnly.MinValue), leave.HalfDay, p.AcademicYearId));
					result.Add(new()
					{
						["requestNo"] = leave.PublicId,
						["__employeeId"] = e.Id,
						["employeeCode"] = e.EmployeeCode,
						["employeeName"] = (e.FirstName + " " + e.LastName).Trim(),
						["department"] = e.Department,
						["leaveType"] = (leave.ApprovedPaidType ?? leave.PaidType).ToString(),
						["startDate"] = leave.StartDateOnly,
						["endDate"] = leave.EndDateOnly,
						["status"] = leave.Status.ToString(),
						["approvedUnits"] = units,
						["description"] = leave.Description
					});
				}
				return result;
			}
			if ((long) staff.Count * (to.DayNumber - from.DayNumber + 1) > HrReportingEngine.MaximumPopulation) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
			var shiftIds = staff.Where(x => x.ShiftId.HasValue).Select(x => x.ShiftId!.Value).Distinct().ToArray();
			var shiftLookup = (await executor.ToListAsync((await shifts.GetQueryableAsync()).Where(x => Enumerable.Contains(shiftIds, x.Id)))).ToDictionary(x => x.Id);
			var staffShifts = staff.ToDictionary(x => x.Id, x => x.ShiftId.HasValue ? shiftLookup.GetValueOrDefault(x.ShiftId.Value) : null);
			var register = await attendanceReader.BuildRowsAsync(new GetAttendanceRegisterInput { AcademicYearId = p.AcademicYearId, EducationMediumStageId = p.EducationMediumStageId }, from, to, scope, staffIds, includeWeeklyOffs: true);
			var approved = leaves.Where(x => x.Status == EmployeeLeaveStatusType.Approved).ToLookup(x => x.EmployeeId);
			return register.Select(x =>
			{
				var shift = staffShifts.GetValueOrDefault(x.EmployeeId);
				var dayState = HrReportingAttendanceDay.State(x.AttendanceDate, now, timezone, shift?.StartTime, shift?.EndTime);
				var dayLeaves = approved[x.EmployeeId].Where(l => l.StartDateOnly <= x.AttendanceDate && l.EndDateOnly >= x.AttendanceDate).ToList();
				var isLeave = dayLeaves.Any();
				var leaveClassification = dayLeaves.Any(l => !l.HalfDay) ? "Approved leave" : "Half-day approved leave";
				var classification = x.IsHoliday ? x.HolidayName == "Weekly off" ? "Non-working day" : "Holiday" : isLeave ? leaveClassification : dayState == "Future" ? "Future" : x.Status == AttendanceRegisterStatus.NotMarked ? dayState == "Open" ? "Open day" : "Not recorded" : x.Status.ToString();
				var exception = x.IsHoliday || isLeave || dayState != "Closed" ? "" : !x.IsMarked ? "Not recorded" : IsMissingPunch(x) ? "Missing punch" : "";
				var punchIssue = exception != "Missing punch" ? "" : !x.TimeIn.HasValue && !x.TimeOut.HasValue ? "Missing both punches" : !x.TimeIn.HasValue ? "Missing in-punch" : "Missing out-punch";
				return new Dictionary<string, object?>
				{
					["__employeeId"] = x.EmployeeId,
					["employeeId"] = x.EmployeeId.ToString(),
					["attendanceDate"] = x.AttendanceDate,
					["employeeCode"] = x.EmployeeCode,
					["employeeName"] = x.EmployeeName,
					["department"] = x.Department,
					["status"] = x.Status.ToString(),
					["classification"] = classification,
					["exception"] = exception,
					["timeIn"] = x.TimeIn,
					["timeOut"] = x.TimeOut,
					["hoursWorked"] = x.TimeIn.HasValue && x.TimeOut.HasValue ? (decimal?) (x.TimeOut.Value - x.TimeIn.Value).TotalHours : null,
					["periodState"] = dayState,
					["punchIssue"] = punchIssue,
					["remarks"] = x.Remarks,
					["holiday"] = x.HolidayName
				};
			}).ToList();
		}
		var metadata = HrReportingCatalogue.Datasets().Single(x => x.Id == d.DataSource);
		var all = new List<Dictionary<string, object?>>();
		var departmentsByCode = staff.ToDictionary(x => x.EmployeeCode, x => x.Department);
		var idsByCode = staff.ToDictionary(x => x.EmployeeCode, x => x.Id);
		var request = new HrReportRequestDto
		{
			ReportType = d.DataSource,
			DataSource = d.DataSource,
			PageSize = 200,
			Filters = new() { EmployeeIds = staffIds.ToList(), FromDate = p.FromDate, ToDate = p.ToDate, Month = p.Month, IncludeDrafts = p.IncludeDrafts, TimeZone = p.TimeZone }
		};
		for (var page = 1; ; page++)
		{
			if (ensureActive != null && page % 10 == 1) await ensureActive();
			request.Page = page;
			var batch = await sourceReader.BuildRowsResultAsync(d.DataSource, request, scope, false);
			warnings.AddRange(batch.Warnings);
			if (batch.TotalCount > HrReportingEngine.MaximumPopulation || !batch.IsDatabaseBacked) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
			foreach (var row in batch.Rows)
			{
				var typed = metadata.Fields.ToDictionary(f => f.Id, f => HrReportingEngine.Parse(row.GetValueOrDefault(f.Id), f.Type));
				if (row.TryGetValue("__components", out var components) && components != null) typed["__components"] = JsonSerializer.Deserialize<List<HrReportingSavedComponent>>(components, HrReportingEngine.Json)!;
				if (d.DataSource.StartsWith("payroll_", StringComparison.Ordinal) && typed.GetValueOrDefault("status") is "Preview") typed["status"] = "Draft";
				if (row.TryGetValue("employeeCode", out var code) && code != null && idsByCode.TryGetValue(code, out var employeeId)) { typed["department"] = departmentsByCode.GetValueOrDefault(code); typed["__employeeId"] = employeeId; }
				if (row.TryGetValue("__employeeId", out var savedId) && Guid.TryParse(savedId, out var id) && staffById.TryGetValue(id, out var employee)) { typed["__employeeId"] = id; typed["department"] = employee.Department; }
				if (typed.TryGetValue("bankAccount", out var account) && account is string bank) typed["bankAccount"] = bank.Length > 4 ? new string('*', bank.Length - 4) + bank[^4..] : "****";
				all.Add(typed);
			}
			if (all.Count >= batch.TotalCount || batch.Rows.Count == 0) break;
		}
		return all;
	}
	private static bool InRange(object? value, HrReportParametersDto p) => value is DateOnly date && date >= p.FromDate && date <= p.ToDate;
}
