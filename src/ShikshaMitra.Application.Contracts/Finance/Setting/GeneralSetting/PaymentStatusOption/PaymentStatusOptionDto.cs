using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;

public class PaymentStatusOptionDto: AuditedEntityDto<Guid>
{
	public string PaymentStatusOptionName { get; set; }
}
