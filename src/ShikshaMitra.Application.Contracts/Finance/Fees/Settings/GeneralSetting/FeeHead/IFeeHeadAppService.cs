using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
namespace ShikshaMitra.Finance.Fees.Settings.GeneralSetting.FeeHead;


public interface IFeeHeadAppService : ICrudAppService<FeeHeadDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateFeeHeadDto>
{

}
