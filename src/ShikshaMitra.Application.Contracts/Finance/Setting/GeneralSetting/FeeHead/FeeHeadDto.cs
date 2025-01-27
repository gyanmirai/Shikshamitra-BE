using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;

public class FeeHeadDto : AuditedEntityDto<Guid>
{
	public string FeeHeadName { get; set; }
}


