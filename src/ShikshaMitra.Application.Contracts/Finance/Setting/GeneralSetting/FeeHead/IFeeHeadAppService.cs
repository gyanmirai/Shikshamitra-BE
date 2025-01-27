using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;


public interface IFeeHeadAppService : ICrudAppService<FeeHeadDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateFeeHeadDto>
{

}
