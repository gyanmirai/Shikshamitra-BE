using System;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Entities.Auditing;


namespace ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;

public class PaymentStatusOption : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string PaymentStatusOptionName { get; private set; }

	protected PaymentStatusOption() { } // For ORM

	internal PaymentStatusOption(Guid id, Guid? tenantId, string paymentStatusOptionName) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		PaymentStatusOptionName = paymentStatusOptionName;
	}
}
