using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IProvidentFundAppService : ICrudAppService<ProvidentFundDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProvidentFundDto>
{
	Task<PagedResultDto<ProvidentFundDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search);
}
