using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;

public class FeeHeadDto : AuditedEntityDto<Guid>
{
	public string FeeHeadName { get; set; }
}


