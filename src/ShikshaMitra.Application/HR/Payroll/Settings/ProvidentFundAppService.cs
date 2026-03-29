using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class ProvidentFundAppService(IProvidentFundRepository providentFundRepository)
	: CrudAppService<HR.Payroll.Settings.ProvidentFund, ProvidentFundDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProvidentFundDto>(providentFundRepository),
		IProvidentFundAppService
{
	public async Task<PagedResultDto<ProvidentFundDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search)
	{
		var queryable = await Repository.GetQueryableAsync();

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLower();
			queryable = queryable.Where(x =>
				x.Name.ToLower().Contains(term) ||
				x.Code.ToLower().Contains(term));
		}

		queryable = queryable.OrderBy(x => x.Name);

		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(skipCount)
				.Take(maxResultCount)
		);

		return new PagedResultDto<ProvidentFundDto>(
			totalCount,
			ObjectMapper.Map<List<ProvidentFund>, List<ProvidentFundDto>>(items)
		);
	}
}
