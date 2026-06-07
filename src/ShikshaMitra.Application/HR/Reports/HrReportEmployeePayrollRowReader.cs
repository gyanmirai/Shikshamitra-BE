using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

using static HrReportRowFormatting;

public class HrReportEmployeePayrollRowReader(
	IRepository<PayrollRunItemSnapshot, Guid> payrollSnapshotRepository,
	HrReportEmployeeQueryService employeeQueryService,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> BuildEmployeeRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var query = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		query = ApplyEmployeeDateRange(query, input.Filters);
		var employees = await asyncExecuter.ToListAsync(query.OrderBy(e => e.EmployeeCode).Select(e => new
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
		}).Take(HrReportRequestProcessor.MaxExportRows));

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

	private static IQueryable<Employee> ApplyEmployeeDateRange(IQueryable<Employee> query, HrReportFilterDto filters)
	{
		if (!filters.FromDate.HasValue && !filters.ToDate.HasValue)
		{
			return query;
		}

		var dateField = string.IsNullOrWhiteSpace(filters.DateField) ? "dateOfJoining" : filters.DateField;
		return dateField switch
		{
			"dateOfBirth" => ApplyNullableDateRange(query, filters, e => e.DateOfBirth),
			_ => ApplyNullableDateRange(query, filters, e => e.DateOfJoining)
		};
	}

	private static IQueryable<Employee> ApplyNullableDateRange(
		IQueryable<Employee> query,
		HrReportFilterDto filters,
		System.Linq.Expressions.Expression<Func<Employee, DateOnly?>> selector)
	{
		if (filters.FromDate.HasValue)
		{
			query = query.Where(BuildNullableDateComparison(selector, filters.FromDate.Value, true));
		}

		if (filters.ToDate.HasValue)
		{
			query = query.Where(BuildNullableDateComparison(selector, filters.ToDate.Value, false));
		}

		return query;
	}

	private static System.Linq.Expressions.Expression<Func<Employee, bool>> BuildNullableDateComparison(
		System.Linq.Expressions.Expression<Func<Employee, DateOnly?>> selector,
		DateOnly value,
		bool gte)
	{
		var hasValue = System.Linq.Expressions.Expression.Property(selector.Body, nameof(Nullable<DateOnly>.HasValue));
		var actual = System.Linq.Expressions.Expression.Property(selector.Body, nameof(Nullable<DateOnly>.Value));
		var comparison = gte
			? System.Linq.Expressions.Expression.GreaterThanOrEqual(actual, System.Linq.Expressions.Expression.Constant(value))
			: System.Linq.Expressions.Expression.LessThanOrEqual(actual, System.Linq.Expressions.Expression.Constant(value));
		return System.Linq.Expressions.Expression.Lambda<Func<Employee, bool>>(
			System.Linq.Expressions.Expression.AndAlso(hasValue, comparison),
			selector.Parameters);
	}

	public async Task<List<Dictionary<string, string?>>> BuildPayrollRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var snapshotQuery = await payrollSnapshotRepository.GetQueryableAsync();

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
			}).OrderBy(x => x.EmployeeCode).Take(HrReportRequestProcessor.MaxExportRows));

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
}
