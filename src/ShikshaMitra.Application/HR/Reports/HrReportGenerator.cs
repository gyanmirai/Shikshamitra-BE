using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
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
	IRepository<PayrollRunItemSnapshot, Guid> payrollSnapshotRepository,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	private const int MaxPreviewPageSize = 200;
	private const int MaxExportRows = 20000;

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
			return IsPayrollReport(input.ReportType)
				? await GeneratePayrollReportAsync(input, scope, exportAllRows)
				: await GenerateEmployeeReportAsync(input, scope, exportAllRows);
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
				ReportTypes = GetAllReportTypes()
					.Select(x => new HrReportOptionDto { Id = x, Label = x })
					.ToList(),
				Departments = departments
					.Select(x => new HrReportOptionDto { Id = x, Label = x })
					.ToList(),
				Statuses = Enum.GetValues<EmployeeStatusType>()
					.Select(x => new HrReportOptionDto { Id = ((int)x).ToString(), Label = x.ToString() })
					.ToList()
			};
		}
	}

	private async Task<HrReportPreviewDto> GenerateEmployeeReportAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var query = await BuildScopedEmployeeQueryAsync(scope);
		query = ApplyEmployeeFilters(query, input.Filters);

		var totalCount = await asyncExecuter.CountAsync(query);
		IQueryable<Employee> rowsQuery = query
			.OrderBy(x => x.EmployeeCode)
			.ThenBy(x => x.FirstName)
			.ThenBy(x => x.LastName);

		if (!exportAllRows)
		{
			rowsQuery = rowsQuery.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize);
		}
		else
		{
			rowsQuery = rowsQuery.Take(MaxExportRows);
		}

		var employees = await asyncExecuter.ToListAsync(rowsQuery.Select(e => new EmployeeReportRow
		{
			Id = e.Id,
			EmployeeCode = e.EmployeeCode,
			FirstName = e.FirstName,
			LastName = e.LastName,
			EmployeeName = e.EmployeeName,
			Email = e.Email,
			MobileNo = e.MobileNo,
			DateOfBirth = e.DateOfBirth,
			DateOfJoining = e.DateOfJoining,
			Department = e.Department,
			Designation = e.Designation,
			EmployeeStatusType = e.EmployeeStatusType,
			GenderType = e.GenderType
		}));

		var columns = GetEmployeeColumns(input.ReportType);
		return new HrReportPreviewDto
		{
			ReportType = input.ReportType,
			TotalCount = totalCount,
			Page = input.Page,
			PageSize = input.PageSize,
			Columns = columns,
			Rows = employees.Select(row => ToEmployeeDictionary(row, columns, input.ReportType)).ToList(),
			NormalizedFilters = input.Filters
		};
	}

	private async Task<HrReportPreviewDto> GeneratePayrollReportAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		if (!PayrollMonthNormalizer.TryNormalizeToInternal(input.Filters.Month, out var month))
		{
			throw new BusinessException("HR:Reports:050002")
				.WithData("reason", "Month is required for payroll reports.");
		}

		input.Filters.Month = month;

		var employeeQuery = ApplyEmployeeFilters(await BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var snapshotQuery = await payrollSnapshotRepository.GetQueryableAsync();
		if (currentTenant.Id.HasValue)
		{
			snapshotQuery = snapshotQuery.Where(x => x.TenantId == currentTenant.Id);
		}

		var baseQuery =
			from snapshot in snapshotQuery
			join employee in employeeQuery on snapshot.EmployeeId equals employee.Id
			where snapshot.MonthYear == month
			select new PayrollReportRow
			{
				EmployeeId = snapshot.EmployeeId,
				EmployeeCode = snapshot.EmployeeIdentifier,
				EmployeeName = snapshot.EmployeeName,
				Department = employee.Department,
				Designation = employee.Designation,
				BasicSalary = snapshot.BasicSalary,
				GrossSalary = snapshot.GrossSalary,
				TotalDeduction = snapshot.TotalDeduction,
				NetPay = snapshot.NetPay,
				PreviousMonthNetPay = snapshot.PreviousMonthNetPay,
				NetPayDelta = snapshot.NetPayDelta,
				PresentDays = snapshot.PresentDays,
				PayableDays = snapshot.PayableDays,
				ReviewStatus = snapshot.ReviewStatus,
				IsLocked = snapshot.IsLocked,
				EarningComponentsJson = snapshot.EarningComponentsJson,
				DeductionComponentsJson = snapshot.DeductionComponentsJson,
				PayrollAdjustmentsJson = snapshot.PayrollAdjustmentsJson
			};

		if (input.ReportType is HrReportTypes.AllowanceReport or HrReportTypes.DeductionReport)
		{
			return await GenerateComponentPayrollReportAsync(input, baseQuery, exportAllRows);
		}

		var totalCount = await asyncExecuter.CountAsync(baseQuery);
		IQueryable<PayrollReportRow> rowsQuery = baseQuery.OrderBy(x => x.EmployeeCode);
		if (!exportAllRows)
		{
			rowsQuery = rowsQuery.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize);
		}
		else
		{
			rowsQuery = rowsQuery.Take(MaxExportRows);
		}

		var rows = await asyncExecuter.ToListAsync(rowsQuery);
		var columns = GetPayrollColumns(input.ReportType);
		return new HrReportPreviewDto
		{
			ReportType = input.ReportType,
			TotalCount = totalCount,
			Page = input.Page,
			PageSize = input.PageSize,
			Columns = columns,
			Rows = rows.Select(row => ToPayrollDictionary(row, columns, input.ReportType, month)).ToList(),
			NormalizedFilters = input.Filters
		};
	}

	private async Task<HrReportPreviewDto> GenerateComponentPayrollReportAsync(
		HrReportRequestDto input,
		IQueryable<PayrollReportRow> baseQuery,
		bool exportAllRows)
	{
		var snapshots = await asyncExecuter.ToListAsync(baseQuery.OrderBy(x => x.EmployeeCode).Take(MaxExportRows));
		var requestedComponents = (input.Filters.ComponentNames ?? new List<string>())
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var isAllowance = input.ReportType == HrReportTypes.AllowanceReport;
		var rows = new List<Dictionary<string, string?>>();

		foreach (var snapshot in snapshots)
		{
			var components = ParseComponents(isAllowance ? snapshot.EarningComponentsJson : snapshot.DeductionComponentsJson);
			foreach (var component in components)
			{
				if (requestedComponents.Count > 0 && !requestedComponents.Contains(component.Name))
				{
					continue;
				}

				rows.Add(new Dictionary<string, string?>
				{
					["employeeCode"] = snapshot.EmployeeCode,
					["employeeName"] = snapshot.EmployeeName,
					["department"] = snapshot.Department,
					["component"] = component.Name,
					["amount"] = FormatMoney(component.Amount)
				});
			}
		}

		var totalCount = rows.Count;
		if (!exportAllRows)
		{
			rows = rows.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize).ToList();
		}

		return new HrReportPreviewDto
		{
			ReportType = input.ReportType,
			TotalCount = totalCount,
			Page = input.Page,
			PageSize = input.PageSize,
			Columns =
			[
				new() { Id = "employeeCode", Label = "Employee Code" },
				new() { Id = "employeeName", Label = "Employee Name" },
				new() { Id = "department", Label = "Department" },
				new() { Id = "component", Label = isAllowance ? "Allowance" : "Deduction" },
				new() { Id = "amount", Label = "Amount" }
			],
			Rows = rows,
			NormalizedFilters = input.Filters
		};
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
		return query.Where(e => e.OrganizationAssignments.Any(a => accessibleStageIds.Contains(a.EducationMediumStageId)));
	}

	private static IQueryable<Employee> ApplyEmployeeFilters(IQueryable<Employee> query, HrReportFilterDto filters)
	{
		if (filters.EmployeeIds?.Count > 0)
		{
			query = query.Where(e => filters.EmployeeIds.Contains(e.Id));
		}

		if (filters.Departments?.Count > 0)
		{
			var departments = filters.Departments
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Select(x => x.Trim())
				.ToList();
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

	private static void Normalize(HrReportRequestDto input, bool exportAllRows)
	{
		input.ReportType = (input.ReportType ?? string.Empty).Trim();
		input.Format = string.Equals(input.Format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
			? HrReportFormat.Pdf
			: HrReportFormat.Excel;
		input.Page = input.Page <= 0 ? 1 : input.Page;
		input.PageSize = exportAllRows ? MaxExportRows : Math.Clamp(input.PageSize <= 0 ? 50 : input.PageSize, 1, MaxPreviewPageSize);
		input.Filters ??= new HrReportFilterDto();
		input.Filters.Departments = input.Filters.Departments?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		input.Filters.ComponentNames = input.Filters.ComponentNames?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
	}

	private static bool IsPayrollReport(string reportType) => reportType is
		HrReportTypes.SalaryRegister or
		HrReportTypes.EmployeePayslip or
		HrReportTypes.MonthlyPayrollSummary or
		HrReportTypes.AllowanceReport or
		HrReportTypes.DeductionReport or
		HrReportTypes.PayrollReconciliationReport or
		HrReportTypes.LeaveEncashmentReport or
		HrReportTypes.FinalSettlementReport;

	private static List<string> GetAllReportTypes() =>
	[
		HrReportTypes.EmployeeProfile,
		HrReportTypes.EmployeeList,
		HrReportTypes.JoiningLetter,
		HrReportTypes.EmployeeDetail,
		HrReportTypes.EmployeeContactList,
		HrReportTypes.EmployeeAllDetail,
		HrReportTypes.RelievingCertificate,
		HrReportTypes.IncrementCertificate,
		HrReportTypes.ExperienceCertificate,
		HrReportTypes.SalaryRegister,
		HrReportTypes.EmployeePayslip,
		HrReportTypes.MonthlyPayrollSummary,
		HrReportTypes.AllowanceReport,
		HrReportTypes.DeductionReport,
		HrReportTypes.PayrollReconciliationReport,
		HrReportTypes.LeaveEncashmentReport,
		HrReportTypes.FinalSettlementReport
	];

	private static List<HrReportColumnDto> GetEmployeeColumns(string reportType)
	{
		if (reportType == HrReportTypes.EmployeeContactList)
		{
			return
			[
				new() { Id = "employeeCode", Label = "Employee Code" },
				new() { Id = "employeeName", Label = "Employee Name" },
				new() { Id = "mobileNo", Label = "Mobile" },
				new() { Id = "email", Label = "Email" },
				new() { Id = "dateOfBirth", Label = "Date of Birth" }
			];
		}

		if (reportType is HrReportTypes.JoiningLetter or HrReportTypes.RelievingCertificate or HrReportTypes.IncrementCertificate or HrReportTypes.ExperienceCertificate)
		{
			return
			[
				new() { Id = "employeeCode", Label = "Employee Code" },
				new() { Id = "employeeName", Label = "Employee Name" },
				new() { Id = "department", Label = "Department" },
				new() { Id = "designation", Label = "Designation" },
				new() { Id = "dateOfJoining", Label = "Date of Joining" },
				new() { Id = "documentStatus", Label = "Document Status" }
			];
		}

		return
		[
			new() { Id = "employeeCode", Label = "Employee Code" },
			new() { Id = "employeeName", Label = "Employee Name" },
			new() { Id = "department", Label = "Department" },
			new() { Id = "designation", Label = "Designation" },
			new() { Id = "status", Label = "Status" },
			new() { Id = "gender", Label = "Gender" },
			new() { Id = "mobileNo", Label = "Mobile" },
			new() { Id = "email", Label = "Email" },
			new() { Id = "dateOfJoining", Label = "Date of Joining" }
		];
	}

	private static List<HrReportColumnDto> GetPayrollColumns(string reportType)
	{
		var common =
			new List<HrReportColumnDto>
			{
				new() { Id = "month", Label = "Month" },
				new() { Id = "employeeCode", Label = "Employee Code" },
				new() { Id = "employeeName", Label = "Employee Name" },
				new() { Id = "department", Label = "Department" },
				new() { Id = "designation", Label = "Designation" }
			};

		common.AddRange(reportType switch
		{
			HrReportTypes.MonthlyPayrollSummary =>
			[
				new() { Id = "grossSalary", Label = "Gross Salary" },
				new() { Id = "totalDeduction", Label = "Total Deduction" },
				new() { Id = "netPay", Label = "Net Pay" },
				new() { Id = "reviewStatus", Label = "Review Status" }
			],
			HrReportTypes.PayrollReconciliationReport =>
			[
				new() { Id = "previousMonthNetPay", Label = "Previous Net Pay" },
				new() { Id = "netPay", Label = "Current Net Pay" },
				new() { Id = "netPayDelta", Label = "Delta" },
				new() { Id = "reviewStatus", Label = "Review Status" }
			],
			HrReportTypes.LeaveEncashmentReport =>
			[
				new() { Id = "presentDays", Label = "Present Days" },
				new() { Id = "payableDays", Label = "Payable Days" },
				new() { Id = "netPay", Label = "Net Pay" }
			],
			HrReportTypes.FinalSettlementReport =>
			[
				new() { Id = "grossSalary", Label = "Gross Salary" },
				new() { Id = "totalDeduction", Label = "Total Deduction" },
				new() { Id = "netPay", Label = "Final Pay" },
				new() { Id = "isLocked", Label = "Locked" }
			],
			_ =>
			[
				new() { Id = "basicSalary", Label = "Basic Salary" },
				new() { Id = "grossSalary", Label = "Gross Salary" },
				new() { Id = "totalDeduction", Label = "Total Deduction" },
				new() { Id = "netPay", Label = "Net Pay" },
				new() { Id = "payableDays", Label = "Payable Days" },
				new() { Id = "reviewStatus", Label = "Review Status" }
			]
		});

		return common;
	}

	private static Dictionary<string, string?> ToEmployeeDictionary(
		EmployeeReportRow row,
		List<HrReportColumnDto> columns,
		string reportType)
	{
		var values = new Dictionary<string, string?>
		{
			["employeeCode"] = row.EmployeeCode,
			["employeeName"] = DisplayName(row.EmployeeName, row.FirstName, row.LastName),
			["department"] = row.Department,
			["designation"] = row.Designation,
			["status"] = row.EmployeeStatusType?.ToString(),
			["gender"] = row.GenderType?.ToString(),
			["mobileNo"] = row.MobileNo,
			["email"] = row.Email,
			["dateOfBirth"] = FormatDate(row.DateOfBirth),
			["dateOfJoining"] = FormatDate(row.DateOfJoining),
			["documentStatus"] = reportType
		};

		return columns.ToDictionary(column => column.Id, column => values.GetValueOrDefault(column.Id));
	}

	private static Dictionary<string, string?> ToPayrollDictionary(
		PayrollReportRow row,
		List<HrReportColumnDto> columns,
		string reportType,
		string month)
	{
		var values = new Dictionary<string, string?>
		{
			["month"] = month,
			["employeeCode"] = row.EmployeeCode,
			["employeeName"] = row.EmployeeName,
			["department"] = row.Department,
			["designation"] = row.Designation,
			["basicSalary"] = FormatMoney(row.BasicSalary),
			["grossSalary"] = FormatMoney(row.GrossSalary),
			["totalDeduction"] = FormatMoney(row.TotalDeduction),
			["netPay"] = FormatMoney(row.NetPay),
			["previousMonthNetPay"] = FormatMoney(row.PreviousMonthNetPay),
			["netPayDelta"] = FormatMoney(row.NetPayDelta),
			["presentDays"] = FormatDecimal(row.PresentDays),
			["payableDays"] = FormatDecimal(row.PayableDays),
			["reviewStatus"] = row.ReviewStatus,
			["isLocked"] = row.IsLocked ? "Yes" : "No"
		};

		return columns.ToDictionary(column => column.Id, column => values.GetValueOrDefault(column.Id));
	}

	private static List<ComponentLine> ParseComponents(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new List<ComponentLine>();
		}

		try
		{
			return JsonSerializer.Deserialize<List<ComponentLine>>(json, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			}) ?? new List<ComponentLine>();
		}
		catch (JsonException)
		{
			return new List<ComponentLine>();
		}
	}

	private static string DisplayName(string? employeeName, string? firstName, string? lastName)
	{
		if (!string.IsNullOrWhiteSpace(employeeName))
		{
			return employeeName;
		}

		return string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
	}

	private static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd");
	private static string FormatDecimal(decimal? value) => value?.ToString("0.##") ?? string.Empty;
	private static string FormatMoney(decimal? value) => value?.ToString("0.00") ?? string.Empty;

	private sealed class EmployeeReportRow
	{
		public Guid Id { get; set; }
		public string? EmployeeCode { get; set; }
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? EmployeeName { get; set; }
		public string? Email { get; set; }
		public string? MobileNo { get; set; }
		public DateOnly? DateOfBirth { get; set; }
		public DateOnly? DateOfJoining { get; set; }
		public string? Department { get; set; }
		public string? Designation { get; set; }
		public EmployeeStatusType? EmployeeStatusType { get; set; }
		public GenderType? GenderType { get; set; }
	}

	private sealed class PayrollReportRow
	{
		public Guid EmployeeId { get; set; }
		public string EmployeeCode { get; set; } = string.Empty;
		public string EmployeeName { get; set; } = string.Empty;
		public string? Department { get; set; }
		public string? Designation { get; set; }
		public decimal BasicSalary { get; set; }
		public decimal GrossSalary { get; set; }
		public decimal TotalDeduction { get; set; }
		public decimal NetPay { get; set; }
		public decimal? PreviousMonthNetPay { get; set; }
		public decimal? NetPayDelta { get; set; }
		public decimal PresentDays { get; set; }
		public decimal PayableDays { get; set; }
		public string ReviewStatus { get; set; } = string.Empty;
		public bool IsLocked { get; set; }
		public string EarningComponentsJson { get; set; } = "[]";
		public string DeductionComponentsJson { get; set; } = "[]";
		public string PayrollAdjustmentsJson { get; set; } = "[]";
	}

	private sealed class ComponentLine
	{
		public string Name { get; set; } = string.Empty;
		public decimal Amount { get; set; }
	}
}
