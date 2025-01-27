using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;

public class FeeHeadAppService(IFeeHeadRepository feeHeadRepository) : CrudAppService<FeeHead, FeeHeadDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateFeeHeadDto>(feeHeadRepository), IFeeHeadAppService
{

}

