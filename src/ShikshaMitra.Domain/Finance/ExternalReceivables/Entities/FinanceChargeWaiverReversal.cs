using System;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceChargeWaiverReversal : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid WaiverApplicationId { get; private set; }
	public decimal Amount { get; private set; }
	public string Currency { get; private set; } = string.Empty;
	public Guid ReversedByUserId { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	private FinanceChargeWaiverReversal() { }
	public FinanceChargeWaiverReversal(Guid id, Guid tenantId, Guid applicationId, decimal amount,
		string currency, Guid actorId, string reason) : base(id)
	{
		FinanceExternalSettlementAmounts.EnsureMoney(amount, positive: true);
		if (id == Guid.Empty || tenantId == Guid.Empty || applicationId == Guid.Empty || actorId == Guid.Empty ||
			currency != "INR" || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		TenantId = tenantId; WaiverApplicationId = applicationId; Amount = amount;
		Currency = currency; ReversedByUserId = actorId; Reason = reason.Trim();
	}
}
