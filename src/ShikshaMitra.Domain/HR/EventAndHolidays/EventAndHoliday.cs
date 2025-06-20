using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.EventAndHolidays;

public class EventAndHoliday : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateTimeOffset StartDate { get; private set; }
	public DateTimeOffset EndDate { get; private set; }
	public string Name { get; set; }
	public string? Reason { get; private set; }
	public bool IsHoliday { get; private set; }
	protected EventAndHoliday() { }
	
	internal EventAndHoliday(Guid id, Guid? tenantId, DateTimeOffset  startDate, DateTimeOffset endDate, string name, string? reason, bool isHoliday ) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		StartDate = startDate;
		EndDate = endDate;
		Name = name;
		Reason = reason;
		IsHoliday = isHoliday;
	}
}

