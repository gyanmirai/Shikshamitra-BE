using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.EventAndHolidays;

public class EventAndHoliday : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateTimeOffset StartDate { get; private set; }
	public DateTimeOffset EndDate { get; private set; }
	public string Reason { get; private set; }
	
	protected EventAndHoliday() { }
	
	internal EventAndHoliday(Guid id, Guid? tenantId, DateTimeOffset  startDate, DateTimeOffset endDate, string reason ) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		StartDate = startDate;
		EndDate = endDate;
		Reason = reason;
	}
}

