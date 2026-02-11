using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;

[RemoteService(false)]
public class FeeHeadAppService(IFeeHeadRepository feeHeadRepository) : CrudAppService<FeeHead, FeeHeadDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateFeeHeadDto>(feeHeadRepository), IFeeHeadAppService
{

}

