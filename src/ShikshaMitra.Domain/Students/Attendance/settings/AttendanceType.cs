using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance.settings;

public class AttendanceType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Type { get; private set; }
	public string Color { get; private set; }
	
	protected AttendanceType() { } // For ORM
	
	internal AttendanceType(Guid id, Guid? tenantId, string type, string color) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		Type = type;
		Color = color;
	}

}