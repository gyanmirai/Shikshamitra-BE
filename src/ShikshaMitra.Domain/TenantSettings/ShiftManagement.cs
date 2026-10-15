using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagement : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string ShiftName { get; set; }
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
}
