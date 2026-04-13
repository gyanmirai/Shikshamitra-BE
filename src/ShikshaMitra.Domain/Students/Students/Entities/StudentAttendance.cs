using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentAttendance : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateTimeOffset AttendanceDate { get; private set; }
	public AttendanceStatusEnum Status { get; private set; }
	public string? Remarks { get; private set; }
}
