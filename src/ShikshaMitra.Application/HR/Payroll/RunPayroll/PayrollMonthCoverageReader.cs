using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollMonthCoverageReader(ISalarySlipRepository slips, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<IQueryable<SalarySlip>> GetIssuedAsync(string month, IQueryable<Employee> staff)
	{
		var scope = staff.Select(x => x.Id);
		return (await slips.GetQueryableAsync()).Where(x => x.MonthYear == month && scope.Contains(x.EmployeeId) &&
			(x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid));
	}

	internal async Task<(int EligibleOrIssued, int Issued)> ReadAsync(string month, IQueryable<Employee> staff)
	{
		var (year, number, period) = PayrollRunRules.ParseMonth(month);
		var eligible = await PayrollScopedEmployeeResolver.ReadEligibleIdsAsync(staff, year, number, queries);
		var issued = await queries.ToListAsync((await GetIssuedAsync(period, staff)).Select(x => x.EmployeeId).Distinct());
		return (eligible.Union(issued).Count(), issued.Count);
	}
}
