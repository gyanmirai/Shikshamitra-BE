using ShikshaMitra.Common;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public class HrReportGenerator(
	IEmployeeRepository employeeRepository,
	IRepository<EmployeeAttendance, Guid> attendanceRepository,
	IRepository<EmployeeLeave, Guid> leaveRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollSnapshotRepository,
	IRepository<HrPolicyAssignment, Guid> policyAssignmentRepository,
	IRepository<HrAuditEvent, Guid> auditRepository,
	IRepository<PerformanceReviewAssignment, Guid> performanceAssignmentRepository,
	IRepository<ProfessionalReviewCycle, Guid> reviewCycleRepository,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	EmployeeOrganizationScopeResolver organizationScopeResolver,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	private const int MaxPreviewPageSize = 200;
	private const int MaxExportRows = 20000;

	public Task<HrReportSourcesDto> GetSourcesAsync()
	{
		return Task.FromResult(new HrReportSourcesDto
		{
			Sources = BuildSources(),
			Operators =
			[
				new() { Id = HrReportFilterOperator.Equals, Label = "Equals", Types = ["string", "number", "date", "boolean"] },
				new() { Id = HrReportFilterOperator.Contains, Label = "Contains", Types = ["string"] },
				new() { Id = HrReportFilterOperator.In, Label = "Is one of", Types = ["string", "number", "boolean"] },
				new() { Id = HrReportFilterOperator.GreaterThanOrEqual, Label = "On or after / >=", Types = ["date", "number"] },
				new() { Id = HrReportFilterOperator.LessThanOrEqual, Label = "On or before / <=", Types = ["date", "number"] }
			],
			Formats =
			[
				new() { Id = HrReportFormat.Excel, Label = "Excel" },
				new() { Id = HrReportFormat.Pdf, Label = "PDF" }
			]
		});
	}

	public async Task<HrReportPreviewDto> GenerateAsync(
		HrReportRequestDto input,
		Guid userId,
		Guid? tenantId,
		bool exportAllRows)
	{
		Normalize(input, exportAllRows);
		using (currentTenant.Change(tenantId))
		{
			var scope = await userScopeService.GetUserScopeAsync(userId);
			var source = ResolveSource(input);
			var rows = source switch
			{
				HrReportDataSources.Payroll => await BuildPayrollRowsAsync(input, scope),
				HrReportDataSources.Attendance => await BuildAttendanceRowsAsync(input, scope),
				HrReportDataSources.Leave => await BuildLeaveRowsAsync(input, scope),
				HrReportDataSources.Policies => await BuildPolicyRowsAsync(input),
				HrReportDataSources.Performance => await BuildPerformanceRowsAsync(input, scope),
				_ => await BuildEmployeeRowsAsync(input, scope)
			};

			rows = ApplyConditionFilters(rows, input.Filters.Conditions);
			rows = ApplyGrouping(rows, input.Groups, input.Summaries);
			rows = ApplySort(rows, input.Sorts);

			var totalCount = rows.Count;
			var columns = ResolveColumns(source, input, rows);
			var pageRows = exportAllRows
				? rows.Take(MaxExportRows).ToList()
				: rows.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize).ToList();

			return new HrReportPreviewDto
			{
				DataSource = source,
				ReportType = string.IsNullOrWhiteSpace(input.ReportType) ? GetSource(source).Label : input.ReportType,
				TotalCount = totalCount,
				Page = input.Page,
				PageSize = input.PageSize,
				Columns = columns,
				Rows = pageRows.Select(row => columns.ToDictionary(column => column.Id, column => row.GetValueOrDefault(column.Id))).ToList(),
				NormalizedFilters = input.Filters
			};
		}
	}

	public async Task<HrReportOptionsDto> GetOptionsAsync(Guid userId, Guid? tenantId)
	{
		using (currentTenant.Change(tenantId))
		{
			var scope = await userScopeService.GetUserScopeAsync(userId);
			var employeeQuery = await BuildScopedEmployeeQueryAsync(scope);
			var departments = await asyncExecuter.ToListAsync(
				employeeQuery
					.Where(e => e.Department != null && e.Department != string.Empty)
					.Select(e => e.Department!)
					.Distinct()
					.OrderBy(x => x));

			return new HrReportOptionsDto
			{
				ReportTypes = (await GetSourcesAsync()).Sources.Select(x => new HrReportOptionDto { Id = x.Id, Label = x.Label }).ToList(),
				Departments = departments.Select(x => new HrReportOptionDto { Id = x, Label = x }).ToList(),
				Statuses = Enum.GetValues<EmployeeStatusType>().Select(x => new HrReportOptionDto { Id = ((int)x).ToString(), Label = x.ToString() }).ToList()
			};
		}
	}

	private async Task<List<Dictionary<string, string?>>> BuildEmployeeRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var query = ApplyEmployeeFilters(await BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var employees = await asyncExecuter.ToListAsync(query.OrderBy(e => e.EmployeeCode).Take(MaxExportRows).Select(e => new
		{
			e.EmployeeCode,
			e.EmployeeName,
			e.FirstName,
			e.LastName,
			e.Department,
			e.Designation,
			e.EmployeeStatusType,
			e.GenderType,
			e.MobileNo,
			e.Email,
			e.DateOfJoining,
			e.DateOfBirth
		}));

		return employees.Select(e => Row(
			("employeeCode", e.EmployeeCode),
			("employeeName", DisplayName(e.EmployeeName, e.FirstName, e.LastName)),
			("department", e.Department),
			("designation", e.Designation),
			("status", e.EmployeeStatusType?.ToString()),
			("gender", e.GenderType?.ToString()),
			("mobileNo", e.MobileNo),
			("email", e.Email),
			("dateOfJoining", FormatDate(e.DateOfJoining)),
			("dateOfBirth", FormatDate(e.DateOfBirth)))).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> BuildPayrollRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = ApplyEmployeeFilters(await BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var snapshotQuery = await payrollSnapshotRepository.GetQueryableAsync();
		if (currentTenant.Id.HasValue)
		{
			snapshotQuery = snapshotQuery.Where(x => x.TenantId == currentTenant.Id);
		}

		if (PayrollMonthNormalizer.TryNormalizeToInternal(input.Filters.Month, out var month))
		{
			snapshotQuery = snapshotQuery.Where(x => x.MonthYear == month);
		}

		var rows = await asyncExecuter.ToListAsync((
			from snapshot in snapshotQuery
			join employee in employeeQuery on snapshot.EmployeeId equals employee.Id
			select new
			{
				Month = snapshot.MonthYear,
				EmployeeCode = snapshot.EmployeeIdentifier,
				snapshot.EmployeeName,
				employee.Department,
				employee.Designation,
				snapshot.BasicSalary,
				snapshot.GrossSalary,
				snapshot.TotalDeduction,
				snapshot.NetPay,
				snapshot.PayableDays,
				snapshot.ReviewStatus,
				snapshot.IsLocked
			}).OrderBy(x => x.EmployeeCode).Take(MaxExportRows));

		return rows.Select(r => Row(
			("month", r.Month),
			("employeeCode", r.EmployeeCode),
			("employeeName", r.EmployeeName),
			("department", r.Department),
			("designation", r.Designation),
			("basicSalary", FormatMoney(r.BasicSalary)),
			("grossSalary", FormatMoney(r.GrossSalary)),
			("totalDeduction", FormatMoney(r.TotalDeduction)),
			("netPay", FormatMoney(r.NetPay)),
			("payableDays", FormatDecimal(r.PayableDays)),
			("reviewStatus", r.ReviewStatus),
			("isLocked", r.IsLocked ? "Yes" : "No"))).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> BuildAttendanceRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = ApplyEmployeeFilters(await BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		if (currentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x => x.TenantId == currentTenant.Id);
		}

		attendanceQuery = ApplyDateOnlyRange(attendanceQuery, input.Filters, x => x.AttendanceDate);
		var rows = await asyncExecuter.ToListAsync((
			from attendance in attendanceQuery
			join employee in employeeQuery on attendance.EmployeeId equals employee.Id
			select new
			{
				attendance.AttendanceDate,
				employee.EmployeeCode,
				employee.EmployeeName,
				employee.FirstName,
				employee.LastName,
				employee.Department,
				Status = attendance.StatusType,
				attendance.TimeIn,
				attendance.TimeOut,
				attendance.TotalHoursWorked,
				attendance.Remarks
			}).OrderByDescending(x => x.AttendanceDate).ThenBy(x => x.EmployeeCode).Take(MaxExportRows));

		return rows.Select(r => Row(
			("attendanceDate", FormatDate(r.AttendanceDate)),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("department", r.Department),
			("status", r.Status.ToString()),
			("timeIn", FormatDateTime(r.TimeIn)),
			("timeOut", FormatDateTime(r.TimeOut)),
			("hoursWorked", r.TotalHoursWorked?.ToString("0.##", CultureInfo.InvariantCulture)),
			("remarks", r.Remarks))).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> BuildLeaveRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = ApplyEmployeeFilters(await BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var leaveQuery = await leaveRepository.GetQueryableAsync();
		if (currentTenant.Id.HasValue)
		{
			leaveQuery = leaveQuery.Where(x => x.TenantId == currentTenant.Id);
		}

		leaveQuery = ApplyDateOnlyRange(leaveQuery, input.Filters, x => x.StartDateOnly);
		var rows = await asyncExecuter.ToListAsync((
			from leave in leaveQuery
			join employee in employeeQuery on leave.EmployeeId equals employee.Id
			select new
			{
				leave.PublicId,
				employee.EmployeeCode,
				employee.EmployeeName,
				employee.FirstName,
				employee.LastName,
				LeaveType = leave.LeavePolicy != null ? leave.LeavePolicy.PolicyName : leave.LeaveTypeId.ToString(),
				leave.StartDateOnly,
				leave.EndDateOnly,
				leave.Status,
				leave.ApprovedLeaveUnits,
				leave.Description
			}).OrderByDescending(x => x.StartDateOnly).ThenBy(x => x.EmployeeCode).Take(MaxExportRows));

		return rows.Select(r => Row(
			("requestNo", r.PublicId.ToString(CultureInfo.InvariantCulture)),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("leaveType", r.LeaveType),
			("startDate", FormatDate(r.StartDateOnly)),
			("endDate", FormatDate(r.EndDateOnly)),
			("status", r.Status.ToString()),
			("approvedUnits", FormatDecimal(r.ApprovedLeaveUnits)),
			("description", r.Description))).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> BuildPolicyRowsAsync(HrReportRequestDto input)
	{
		var policyQuery = await policyAssignmentRepository.GetQueryableAsync();
		var auditQuery = await auditRepository.GetQueryableAsync();
		if (currentTenant.Id.HasValue)
		{
			policyQuery = policyQuery.Where(x => x.TenantId == currentTenant.Id);
			auditQuery = auditQuery.Where(x => x.TenantId == currentTenant.Id);
		}

		if (input.Filters.FromDate.HasValue)
		{
			var from = input.Filters.FromDate.Value.ToDateTime(TimeOnly.MinValue);
			policyQuery = policyQuery.Where(x => x.EffectiveFrom >= from);
			auditQuery = auditQuery.Where(x => x.OccurredAt >= from);
		}

		if (input.Filters.ToDate.HasValue)
		{
			var to = input.Filters.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
			policyQuery = policyQuery.Where(x => x.EffectiveFrom <= to);
			auditQuery = auditQuery.Where(x => x.OccurredAt <= to);
		}

		var policyRows = (await asyncExecuter.ToListAsync(policyQuery.OrderByDescending(x => x.EffectiveFrom).Take(MaxExportRows / 2)))
			.Select(x => Row(
				("recordType", "Policy Assignment"),
				("entity", x.TargetType.ToString()),
				("action", x.IsActive ? "Active" : "Inactive"),
				("target", x.TargetDisplayName ?? x.TargetKey),
				("effectiveFrom", FormatDate(x.EffectiveFrom)),
				("effectiveTo", FormatDate(x.EffectiveTo)),
				("isActive", x.IsActive ? "Yes" : "No"),
				("occurredAt", null),
				("summary", x.AssignmentReason)));

		var auditRows = (await asyncExecuter.ToListAsync(auditQuery.OrderByDescending(x => x.OccurredAt).Take(MaxExportRows / 2)))
			.Select(x => Row(
				("recordType", "Audit"),
				("entity", x.EntityType),
				("action", x.Action),
				("target", x.EntityName),
				("effectiveFrom", null),
				("effectiveTo", null),
				("isActive", null),
				("occurredAt", FormatDateTime(x.OccurredAt)),
				("summary", x.Summary)));

		return policyRows.Concat(auditRows).Take(MaxExportRows).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> BuildPerformanceRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = ApplyEmployeeFilters(await BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var assignmentQuery = await performanceAssignmentRepository.GetQueryableAsync();
		var cycleQuery = await reviewCycleRepository.GetQueryableAsync();
		if (currentTenant.Id.HasValue)
		{
			assignmentQuery = assignmentQuery.Where(x => x.TenantId == currentTenant.Id);
			cycleQuery = cycleQuery.Where(x => x.TenantId == currentTenant.Id);
		}

		var rows = await asyncExecuter.ToListAsync((
			from assignment in assignmentQuery
			join employee in employeeQuery on assignment.EmployeeId equals employee.Id
			join cycle in cycleQuery on assignment.CycleId equals cycle.Id
			select new
			{
				Cycle = cycle.Name,
				employee.EmployeeCode,
				employee.EmployeeName,
				employee.FirstName,
				employee.LastName,
				employee.Department,
				assignment.Status,
				assignment.FinalScore,
				assignment.FinalRating,
				assignment.FinalizedAt
			}).OrderBy(x => x.Cycle).ThenBy(x => x.EmployeeCode).Take(MaxExportRows));

		return rows.Select(r => Row(
			("cycle", r.Cycle),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("department", r.Department),
			("status", r.Status.ToString()),
			("finalScore", FormatDecimal(r.FinalScore)),
			("finalRating", r.FinalRating),
			("finalizedAt", FormatDateTime(r.FinalizedAt)))).ToList();
	}

	private async Task<IQueryable<Employee>> BuildScopedEmployeeQueryAsync(UserScopeContext scope)
	{
		var query = await employeeRepository.GetQueryableAsync();
		if (currentTenant.Id.HasValue)
		{
			query = query.Where(e => e.TenantId == currentTenant.Id);
		}

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return query;
		}

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		var accessibleStageIds = await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope);
		var matchingUserIds = await organizationScopeResolver.GetUserIdsWithAnyStageAsync(accessibleStageIds);
		return query.Where(e => e.UserId.HasValue && matchingUserIds.Contains(e.UserId.Value));
	}

	private static IQueryable<Employee> ApplyEmployeeFilters(IQueryable<Employee> query, HrReportFilterDto filters)
	{
		if (filters.EmployeeIds?.Count > 0)
		{
			query = query.Where(e => filters.EmployeeIds.Contains(e.Id));
		}

		if (filters.Departments?.Count > 0)
		{
			var departments = filters.Departments.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
			query = query.Where(e => e.Department != null && departments.Contains(e.Department));
		}

		if (filters.Statuses?.Count > 0)
		{
			var statuses = filters.Statuses.Select(x => (EmployeeStatusType)x).ToList();
			query = query.Where(e => e.EmployeeStatusType.HasValue && statuses.Contains(e.EmployeeStatusType.Value));
		}

		if (filters.GenderType.HasValue)
		{
			var gender = (GenderType)filters.GenderType.Value;
			query = query.Where(e => e.GenderType == gender);
		}

		return query;
	}

	private static IQueryable<T> ApplyDateOnlyRange<T>(IQueryable<T> query, HrReportFilterDto filters, System.Linq.Expressions.Expression<Func<T, DateOnly>> selector)
	{
		if (filters.FromDate.HasValue)
		{
			query = query.Where(BuildDateComparison(selector, filters.FromDate.Value, true));
		}

		if (filters.ToDate.HasValue)
		{
			query = query.Where(BuildDateComparison(selector, filters.ToDate.Value, false));
		}

		return query;
	}

	private static System.Linq.Expressions.Expression<Func<T, bool>> BuildDateComparison<T>(System.Linq.Expressions.Expression<Func<T, DateOnly>> selector, DateOnly value, bool gte)
	{
		var comparison = gte
			? System.Linq.Expressions.Expression.GreaterThanOrEqual(selector.Body, System.Linq.Expressions.Expression.Constant(value))
			: System.Linq.Expressions.Expression.LessThanOrEqual(selector.Body, System.Linq.Expressions.Expression.Constant(value));
		return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(comparison, selector.Parameters);
	}

	private static List<Dictionary<string, string?>> ApplyConditionFilters(List<Dictionary<string, string?>> rows, List<HrReportFilterConditionDto>? conditions)
	{
		if (conditions == null || conditions.Count == 0)
		{
			return rows;
		}

		return rows.Where(row => conditions.All(condition => Matches(row, condition))).ToList();
	}

	private static bool Matches(Dictionary<string, string?> row, HrReportFilterConditionDto condition)
	{
		if (string.IsNullOrWhiteSpace(condition.Field) || !row.TryGetValue(condition.Field, out var raw))
		{
			return true;
		}

		var value = raw ?? string.Empty;
		var expected = condition.Value ?? string.Empty;
		return condition.Operator switch
		{
			HrReportFilterOperator.Contains => value.Contains(expected, StringComparison.OrdinalIgnoreCase),
			HrReportFilterOperator.In => (condition.Values ?? []).Contains(value, StringComparer.OrdinalIgnoreCase),
			HrReportFilterOperator.GreaterThanOrEqual => Compare(value, expected) >= 0,
			HrReportFilterOperator.LessThanOrEqual => Compare(value, expected) <= 0,
			_ => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)
		};
	}

	private static int Compare(string left, string right)
	{
		if (decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var lnum)
			&& decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var rnum))
		{
			return lnum.CompareTo(rnum);
		}

		if (DateTime.TryParse(left, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ldate)
			&& DateTime.TryParse(right, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var rdate))
		{
			return ldate.CompareTo(rdate);
		}

		return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
	}

	private static List<Dictionary<string, string?>> ApplyGrouping(List<Dictionary<string, string?>> rows, List<string>? groups, List<HrReportSummaryDto>? summaries)
	{
		groups = groups?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
		if (groups == null || groups.Count == 0)
		{
			return rows;
		}

		var summaryList = summaries?.Where(x => !string.IsNullOrWhiteSpace(x.Field)).ToList() ?? [];
		return rows
			.GroupBy(row => string.Join("\u001f", groups.Select(group => row.GetValueOrDefault(group) ?? string.Empty)))
			.Select(group =>
			{
				var first = group.First();
				var result = groups.ToDictionary(name => name, name => first.GetValueOrDefault(name));
				result["rowCount"] = group.Count().ToString(CultureInfo.InvariantCulture);
				foreach (var summary in summaryList)
				{
					var key = $"{summary.Function}_{summary.Field}";
					var values = group.Select(row => row.GetValueOrDefault(summary.Field)).Select(ParseDecimal).Where(x => x.HasValue).Select(x => x!.Value).ToList();
					result[key] = summary.Function switch
					{
						HrReportSummaryFunction.Sum => values.Sum().ToString("0.##", CultureInfo.InvariantCulture),
						HrReportSummaryFunction.Average => values.Count == 0 ? "0" : values.Average().ToString("0.##", CultureInfo.InvariantCulture),
						_ => group.Count().ToString(CultureInfo.InvariantCulture)
					};
				}

				return result;
			})
			.ToList();
	}

	private static List<Dictionary<string, string?>> ApplySort(List<Dictionary<string, string?>> rows, List<HrReportSortDto>? sorts)
	{
		sorts = sorts?.Where(x => !string.IsNullOrWhiteSpace(x.Field)).ToList();
		if (sorts == null || sorts.Count == 0)
		{
			return rows;
		}

		IOrderedEnumerable<Dictionary<string, string?>>? ordered = null;
		foreach (var sort in sorts)
		{
			Func<Dictionary<string, string?>, IComparable> key = row => SortValue(row.GetValueOrDefault(sort.Field));
			var desc = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
			ordered = ordered == null
				? (desc ? rows.OrderByDescending(key) : rows.OrderBy(key))
				: (desc ? ordered.ThenByDescending(key) : ordered.ThenBy(key));
		}

		return ordered?.ToList() ?? rows;
	}

	private static void Normalize(HrReportRequestDto input, bool exportAllRows)
	{
		input.ReportType = (input.ReportType ?? string.Empty).Trim();
		input.DataSource = string.IsNullOrWhiteSpace(input.DataSource) ? null : input.DataSource.Trim().ToLowerInvariant();
		input.Format = string.Equals(input.Format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase) ? HrReportFormat.Pdf : HrReportFormat.Excel;
		input.Page = input.Page <= 0 ? 1 : input.Page;
		input.PageSize = exportAllRows ? MaxExportRows : Math.Clamp(input.PageSize <= 0 ? 50 : input.PageSize, 1, MaxPreviewPageSize);
		input.Filters ??= new HrReportFilterDto();
		input.Filters.Departments = input.Filters.Departments?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		input.Filters.ComponentNames = input.Filters.ComponentNames?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		input.Columns = input.Columns?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		input.Groups = input.Groups?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
	}

	private static string ResolveSource(HrReportRequestDto input)
	{
		if (!string.IsNullOrWhiteSpace(input.DataSource))
		{
			return GetSource(input.DataSource).Id;
		}

		return input.ReportType switch
		{
			HrReportTypes.SalaryRegister or HrReportTypes.EmployeePayslip or HrReportTypes.MonthlyPayrollSummary
				or HrReportTypes.AllowanceReport or HrReportTypes.DeductionReport or HrReportTypes.PayrollReconciliationReport
				or HrReportTypes.LeaveEncashmentReport or HrReportTypes.FinalSettlementReport => HrReportDataSources.Payroll,
			_ => HrReportDataSources.Employee
		};
	}

	private static List<HrReportColumnDto> ResolveColumns(string source, HrReportRequestDto input, List<Dictionary<string, string?>> rows)
	{
		var metadata = GetSource(source);
		var requested = input.Groups?.Count > 0
			? input.Groups.Concat(input.Summaries?.Select(x => $"{x.Function}_{x.Field}") ?? []).Prepend("rowCount").Distinct().ToList()
			: input.Columns is { Count: > 0 } ? input.Columns : metadata.DefaultColumns;

		var fields = metadata.Fields.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
		return requested
			.Where(id => rows.Count == 0 || rows[0].ContainsKey(id) || fields.ContainsKey(id) || id == "rowCount" || id.Contains('_'))
			.Select(id => new HrReportColumnDto
			{
				Id = id,
				Label = fields.TryGetValue(id, out var field)
					? field.Label
					: id == "rowCount" ? "Rows" : Humanize(id)
			})
			.ToList();
	}

	private static HrReportSourceDto GetSource(string source)
	{
		return BuildSources().FirstOrDefault(x => x.Id == source.ToLowerInvariant())
			?? BuildSources().First(x => x.Id == HrReportDataSources.Employee);
	}

	private static List<HrReportSourceDto> BuildSources()
	{
		return
		[
			Source(HrReportDataSources.Employee, "Employee", "Employee Reports", "Employee profile, contact, status, department, and reporting-manager data.",
				[
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
					Field("department", "Department", groupable: true),
					Field("designation", "Designation", groupable: true),
					Field("status", "Status", groupable: true),
					Field("gender", "Gender", groupable: true),
					Field("mobileNo", "Mobile"),
					Field("email", "Email"),
					Field("dateOfJoining", "Date of Joining", "date"),
					Field("dateOfBirth", "Date of Birth", "date")
				],
				["employeeCode", "employeeName", "department", "designation", "status", "mobileNo", "email"],
				["employeeCode"]),
			Source(HrReportDataSources.Payroll, "Payroll", "Payroll Reports", "Salary register, payslip, allowance, deduction, reconciliation, and settlement rows.",
				[
					Field("month", "Month", groupable: true),
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
					Field("department", "Department", groupable: true),
					Field("designation", "Designation", groupable: true),
					Field("basicSalary", "Basic Salary", "number", summarizable: true),
					Field("grossSalary", "Gross Salary", "number", summarizable: true),
					Field("totalDeduction", "Total Deduction", "number", summarizable: true),
					Field("netPay", "Net Pay", "number", summarizable: true),
					Field("payableDays", "Payable Days", "number", summarizable: true),
					Field("reviewStatus", "Review Status", groupable: true),
					Field("isLocked", "Locked", "boolean", groupable: true)
				],
				["month", "employeeCode", "employeeName", "department", "grossSalary", "totalDeduction", "netPay", "reviewStatus"],
				["employeeCode"]),
			Source(HrReportDataSources.Attendance, "Attendance", "Attendance Reports", "Daily attendance register and employee-wise status data.",
				[
					Field("attendanceDate", "Date", "date", groupable: true),
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
					Field("department", "Department", groupable: true),
					Field("status", "Status", groupable: true),
					Field("timeIn", "Time In", "date"),
					Field("timeOut", "Time Out", "date"),
					Field("hoursWorked", "Hours Worked", "number", summarizable: true),
					Field("remarks", "Remarks")
				],
				["attendanceDate", "employeeCode", "employeeName", "department", "status", "timeIn", "timeOut", "hoursWorked"],
				["attendanceDate", "employeeCode"]),
			Source(HrReportDataSources.Leave, "Leave", "Leave Reports", "Leave requests, approvals, balances, and leave usage exports.",
				[
					Field("requestNo", "Request #", "number"),
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
					Field("leaveType", "Leave Type", groupable: true),
					Field("startDate", "Start Date", "date"),
					Field("endDate", "End Date", "date"),
					Field("status", "Status", groupable: true),
					Field("approvedUnits", "Approved Units", "number", summarizable: true),
					Field("description", "Description")
				],
				["requestNo", "employeeCode", "employeeName", "leaveType", "startDate", "endDate", "status", "approvedUnits"],
				["startDate"]),
			Source(HrReportDataSources.Policies, "Policies", "Policies And Audit Reports", "Policy assignments and HR audit activity.",
				[
					Field("recordType", "Record Type", groupable: true),
					Field("entity", "Entity"),
					Field("action", "Action", groupable: true),
					Field("target", "Target"),
					Field("effectiveFrom", "Effective From", "date"),
					Field("effectiveTo", "Effective To", "date"),
					Field("isActive", "Active", "boolean", groupable: true),
					Field("occurredAt", "Occurred At", "date"),
					Field("summary", "Summary")
				],
				["recordType", "entity", "action", "target", "effectiveFrom", "effectiveTo", "isActive", "summary"],
				["recordType", "entity"]),
			Source(HrReportDataSources.Performance, "Performance", "Performance Reports", "Review cycles, assignments, scores, outcomes, and calibration views.",
				[
					Field("cycle", "Cycle", groupable: true),
					Field("employeeCode", "Employee Code", groupable: true),
					Field("employeeName", "Employee Name"),
					Field("department", "Department", groupable: true),
					Field("status", "Status", groupable: true),
					Field("finalScore", "Final Score", "number", summarizable: true),
					Field("finalRating", "Final Rating", groupable: true),
					Field("finalizedAt", "Finalized At", "date")
				],
				["cycle", "employeeCode", "employeeName", "department", "status", "finalScore", "finalRating"],
				["cycle", "employeeCode"])
		];
	}

	private static HrReportSourceDto Source(string id, string category, string label, string description, List<HrReportFieldDto> fields, List<string> defaultColumns, List<string> defaultSorts)
	{
		return new HrReportSourceDto
		{
			Id = id,
			Category = category,
			Label = label,
			Description = description,
			Fields = fields,
			DefaultColumns = defaultColumns,
			DefaultSorts = defaultSorts
		};
	}

	private static HrReportFieldDto Field(string id, string label, string type = "string", bool groupable = false, bool summarizable = false)
	{
		return new HrReportFieldDto
		{
			Id = id,
			Label = label,
			Type = type,
			Filterable = true,
			Sortable = true,
			Groupable = groupable,
			Summarizable = summarizable,
			Operators = type switch
			{
				"number" or "date" => [HrReportFilterOperator.Equals, HrReportFilterOperator.GreaterThanOrEqual, HrReportFilterOperator.LessThanOrEqual],
				"boolean" => [HrReportFilterOperator.Equals],
				_ => [HrReportFilterOperator.Equals, HrReportFilterOperator.Contains, HrReportFilterOperator.In]
			}
		};
	}

	private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
		values.ToDictionary(x => x.Key, x => x.Value);

	private static decimal? ParseDecimal(string? value) =>
		decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

	private static IComparable SortValue(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return (0, 0m, DateTime.MinValue, string.Empty);
		}

		if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
		{
			return (1, number, DateTime.MinValue, string.Empty);
		}

		if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
		{
			return (2, 0m, date, string.Empty);
		}

		return (3, 0m, DateTime.MinValue, value);
	}

	private static string DisplayName(string? employeeName, string? firstName, string? lastName) =>
		!string.IsNullOrWhiteSpace(employeeName) ? employeeName : string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

	private static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	private static string? FormatDate(DateTime? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	private static string? FormatDateTime(DateTime? date) => date?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
	private static string? FormatDateTime(DateTimeOffset? date) => date?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
	private static string FormatDecimal(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
	private static string FormatMoney(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
	private static string Humanize(string id) => string.Concat(id.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));
}
