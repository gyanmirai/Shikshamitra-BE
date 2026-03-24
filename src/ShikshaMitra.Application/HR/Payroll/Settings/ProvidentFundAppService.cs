using System;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class ProvidentFundAppService(IProvidentFundRepository providentFundRepository) : CrudAppService<HR.Payroll.Settings.ProvidentFund, ProvidentFundDto,Guid, PagedAndSortedResultRequestDto, CreateUpdateProvidentFundDto>(providentFundRepository), IProvidentFundAppService
{

}