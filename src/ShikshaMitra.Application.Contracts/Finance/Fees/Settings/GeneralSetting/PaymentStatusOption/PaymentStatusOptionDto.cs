using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.GeneralSetting.PaymentStatusOption;

public class PaymentStatusOptionDto: AuditedEntityDto<Guid>
{
	public string PaymentStatusOptionName { get; set; }
}
