using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IProvidentFundAppService : ICrudAppService<ProvidentFundDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProvidentFundDto>
{
	
}