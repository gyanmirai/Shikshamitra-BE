using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class CustomAttendanceRule : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string Name { get; private set; }
	public EmployeeAttendanceStatusType Type { get; private set; }
	public int LateThreshold { get; private set; }
	public TimeUnitType LateThresholdUnit { get; private set; }
	public int HalfDayThreshold { get; private set; }
	public TimeUnitType HalfDayThresholdUnit { get; private set; }
	public bool EnableNotificationForUpperAuthority { get; private set; }

	private CustomAttendanceRule()
	{
	}

	private CustomAttendanceRule(
		Guid id,
		Guid tenantId,
		string name,
		EmployeeAttendanceStatusType type,
		int lateThreshold,
		TimeUnitType lateThresholdUnit,
		int halfDayThreshold,
		TimeUnitType halfDayThresholdUnit,
		bool enableNotificationForUpperAuthority) : base(id)
	{
		TenantId = tenantId;
		Name = name;
		Type = type;
		LateThreshold = lateThreshold;
		LateThresholdUnit = lateThresholdUnit;
		HalfDayThreshold = halfDayThreshold;
		HalfDayThresholdUnit = halfDayThresholdUnit;
		EnableNotificationForUpperAuthority = enableNotificationForUpperAuthority;
	}

	public static CustomAttendanceRule Create(
		Guid id,
		Guid tenantId,
		string name,
		EmployeeAttendanceStatusType type,
		int lateThreshold,
		TimeUnitType lateThresholdUnit,
		int halfDayThreshold,
		TimeUnitType halfDayThresholdUnit,
		bool enableNotificationForUpperAuthority) =>
		new(id, tenantId, name, type, lateThreshold, lateThresholdUnit, halfDayThreshold, halfDayThresholdUnit, enableNotificationForUpperAuthority);

	public void UpdateConfiguration(
		string name,
		EmployeeAttendanceStatusType type,
		int lateThreshold,
		TimeUnitType lateThresholdUnit,
		int halfDayThreshold,
		TimeUnitType halfDayThresholdUnit,
		bool enableNotificationForUpperAuthority)
	{
		Name = name;
		Type = type;
		LateThreshold = lateThreshold;
		LateThresholdUnit = lateThresholdUnit;
		HalfDayThreshold = halfDayThreshold;
		HalfDayThresholdUnit = halfDayThresholdUnit;
		EnableNotificationForUpperAuthority = enableNotificationForUpperAuthority;
	}
}