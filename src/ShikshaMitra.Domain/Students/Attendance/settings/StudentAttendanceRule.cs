using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance.Settings;

public class StudentAttendanceRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; }
	public StudentAttendanceStatusType Type { get; private set; }
	public int LateThreshold { get; private set; }
	public TimeUnitType LateThresholdUnit { get; private set; }
	public int HalfDayThreshold { get; private set; }
	public TimeUnitType HalfDayThresholdUnit { get; private set; }
	public bool EnableNotificationForUpperAuthority { get; private set; }


	protected StudentAttendanceRule()
	{
	}

	internal StudentAttendanceRule(
		Guid id,
		Guid? tenantId,
		string name,
		StudentAttendanceStatusType type,
		int lateThreshold,
		TimeUnitType lateThresholdUnit,
		int halfDayThreshold,
		TimeUnitType halfDayThresholdUnit,
		bool enableNotificationForUpperAuthority) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId)); ;
		Name = name;
		Type = type;
		LateThreshold = lateThreshold;
		LateThresholdUnit = lateThresholdUnit;
		HalfDayThreshold = halfDayThreshold;
		HalfDayThresholdUnit = halfDayThresholdUnit;
		EnableNotificationForUpperAuthority = enableNotificationForUpperAuthority;
	}

}