using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class PayrollPolicyReader(
	IRepository<PayrollPolicy, Guid> repository) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<PayrollPolicyDto>> GetListAsync(PolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (input.IsArchived.HasValue)
		{
			query = input.IsArchived.Value
				? query.Where(x => x.Status == HrPolicyStatus.Archived)
				: query.Where(x => x.Status != HrPolicyStatus.Archived);
		}
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.Name.ToLower().Contains(search));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.IsDefault)
			.ThenBy(x => x.Name)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		return new PagedResultDto<PayrollPolicyDto>(total, rows.Select(HrPolicyMapping.ToDto).ToList());
	}

	public async Task<PayrollPolicyDto> GetAsync(Guid id) =>
		HrPolicyMapping.ToDto(await repository.GetAsync(id));
}
