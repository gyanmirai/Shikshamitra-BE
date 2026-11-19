using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

public class StudentAttendanceOperation : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AttendanceDayId { get; private set; }
	public Guid ActorId { get; private set; }
	public string RequestHash { get; private set; } = "";
	public string ResultJson { get; private set; } = "";
	protected StudentAttendanceOperation() { }
	public StudentAttendanceOperation(Guid id, Guid? tenantId, Guid dayId, Guid actorId, string hash, string result) : base(id)
	{ TenantId = tenantId; AttendanceDayId = dayId; ActorId = actorId; RequestHash = hash; ResultJson = result; }
}
