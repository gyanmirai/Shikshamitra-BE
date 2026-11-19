using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.HostTenants;

// Host-owned history. TargetTenantId is deliberately not an IMultiTenant filter key.
public class TenantStatusChange : CreationAuditedAggregateRoot<Guid>
{
	public Guid TargetTenantId { get; private set; }
	public bool PreviousStatus { get; private set; }
	public bool IsActive { get; private set; }
	public string? Reason { get; private set; }
	private TenantStatusChange() { }
	public TenantStatusChange(Guid id, Guid targetTenantId, bool previousStatus, bool isActive, string? reason) : base(id)
	{
		TargetTenantId = targetTenantId;
		PreviousStatus = previousStatus;
		IsActive = isActive;
		Reason = reason?.Trim();
		if (!isActive && string.IsNullOrWhiteSpace(Reason)) throw new BusinessException(HostTenantConsts.ReasonRequired);
		Check.Length(Reason, nameof(reason), HostTenantConsts.MaxReasonLength);
	}
}
