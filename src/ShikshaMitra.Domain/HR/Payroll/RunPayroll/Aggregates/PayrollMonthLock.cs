using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollMonthLock : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string MonthYear { get; private set; } = string.Empty;
	public Guid OwnerId { get; private set; }
	public DateTime ExpiresAt { get; private set; }
	public void Renew(DateTime expiresAt) => ExpiresAt = expiresAt;

	private PayrollMonthLock()
	{
	}

	public PayrollMonthLock(
		Guid id,
		Guid? tenantId,
		string monthYear,
		Guid ownerId,
		DateTime expiresAt) : base(id)
	{
		TenantId = tenantId;
		MonthYear = monthYear;
		OwnerId = ownerId;
		ExpiresAt = expiresAt;
	}
}
