using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SalarySlipSnapshotReader(
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<PayrollRunItemSnapshot?> GetAsync(Guid employeeId, string monthYear)
	{
		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();

		return await asyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.EmployeeId == employeeId && x.MonthYear == monthYear));
	}
}
