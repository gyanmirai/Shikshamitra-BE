using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollAdjustmentReader(
	IRepository<PayrollAdjustment, Guid> payrollAdjustmentRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<PayrollAdjustmentDto>> GetListAsync(string monthYear, IReadOnlyCollection<Guid> employeeIds)
	{
		var adjustments = await GetByEmployeeAsync(monthYear, employeeIds);
		return adjustments.Values
			.SelectMany(x => x)
			.OrderBy(x => x.EmployeeId)
			.ThenBy(x => x.CreationTime)
			.Select(PayrollRunProjection.ToAdjustmentDto)
			.ToList();
	}

	public async Task<Dictionary<Guid, List<PayrollAdjustment>>> GetByEmployeeAsync(
		string monthYear,
		IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<Guid, List<PayrollAdjustment>>();
		}

		var query = await payrollAdjustmentRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(query.Where(x =>
				x.MonthYear == monthYear &&
				employeeIds.Contains(x.EmployeeId))))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreationTime).ToList());
	}

	public async Task<PayrollAdjustment> GetOrThrowAsync(Guid id)
	{
		var query = await payrollAdjustmentRepository.GetQueryableAsync();
		return await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
			?? throw new BusinessException(HRErrorCodes.PayrollAdjustmentNotFound);
	}
}
