using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;

public class LibraryTiming : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public DayOfWeek Day { get; set; }
	public TimeSpan OpeningTime { get; set; }
	public TimeSpan ClosingTime { get; set; }
	public LibraryTiming(Guid id, DayOfWeek day, TimeSpan openingTime, TimeSpan closingTime, Guid? tenantId)
		: base(id)
	{
		Day = day;
		OpeningTime = openingTime;
		ClosingTime = closingTime;
		TenantId = tenantId;
	}
}
