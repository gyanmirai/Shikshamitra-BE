using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagement : AggregateRoot<Guid>
{
	public string ShiftName { get; set; }
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
}
