using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
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
