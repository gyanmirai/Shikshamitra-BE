using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class SalaryStructureMasterResolver(
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<Dictionary<Guid, EarningComponent>> GetEarningMasterByIdAsync(IEnumerable<SalaryComponentDto> components)
	{
		var ids = ResolveMasterIds(components);
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, EarningComponent>();
		}

		var masters = await asyncExecuter.ToListAsync(
			(await earningComponentRepository.GetQueryableAsync()).Where(x => x.IsActive && ids.Contains(x.Id)));
		return masters.ToDictionary(x => x.Id);
	}

	public async Task<Dictionary<Guid, DeductionComponent>> GetDeductionMasterByIdAsync(IEnumerable<SalaryComponentDto> components)
	{
		var ids = ResolveMasterIds(components);
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, DeductionComponent>();
		}

		var masters = await asyncExecuter.ToListAsync(
			(await deductionComponentRepository.GetQueryableAsync()).Where(x => x.IsActive && ids.Contains(x.Id)));
		return masters.ToDictionary(x => x.Id);
	}

	private static List<Guid> ResolveMasterIds(IEnumerable<SalaryComponentDto> components) =>
		components
			.Where(x => x.ComponentId.HasValue)
			.Select(x => x.ComponentId!.Value)
			.Distinct()
			.ToList();
}
