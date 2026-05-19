using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendancePolicyOverride : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AttendanceConfigurationId { get; protected set; }
	public AttendancePolicyOverrideTargetType TargetType { get; protected set; }
	public string TargetId { get; protected set; } = string.Empty;
	public string TargetName { get; protected set; } = string.Empty;
	public bool IsActive { get; protected set; }
	public int Priority { get; protected set; }
	public double? HoursBasedPayPercentage { get; protected set; }
	public int? GraceTime { get; protected set; }
	public int? MaxLateAllowedPerMonth { get; protected set; }
	public AttendancePenaltyType? PenaltyType { get; protected set; }
	public bool? OvertimeEnabled { get; protected set; }
	public AttendanceOvertimeRate? OvertimeRate { get; protected set; }
	public int? MinOvertimeMinutes { get; protected set; }

	protected AttendancePolicyOverride()
	{
	}

	public AttendancePolicyOverride(
		Guid id,
		Guid? tenantId,
		Guid attendanceConfigurationId,
		AttendancePolicyOverrideTargetType targetType,
		string targetId,
		string targetName,
		bool isActive,
		double? hoursBasedPayPercentage,
		int? graceTime,
		int? maxLateAllowedPerMonth,
		AttendancePenaltyType? penaltyType,
		bool? overtimeEnabled,
		AttendanceOvertimeRate? overtimeRate,
		int? minOvertimeMinutes) : base(id)
	{
		TenantId = tenantId;
		AttendanceConfigurationId = attendanceConfigurationId;
		TargetType = targetType;
		TargetId = targetId;
		TargetName = targetName;
		IsActive = isActive;
		Priority = targetType switch
		{
			AttendancePolicyOverrideTargetType.Shift => 2,
			AttendancePolicyOverrideTargetType.Role => 3,
			AttendancePolicyOverrideTargetType.Department => 4,
			_ => 4
		};
		HoursBasedPayPercentage = hoursBasedPayPercentage;
		GraceTime = graceTime;
		MaxLateAllowedPerMonth = maxLateAllowedPerMonth;
		PenaltyType = penaltyType;
		OvertimeEnabled = overtimeEnabled;
		OvertimeRate = overtimeEnabled == true ? overtimeRate : null;
		MinOvertimeMinutes = overtimeEnabled == true ? minOvertimeMinutes : null;

		Validate();
	}

	private void Validate()
	{
		if (string.IsNullOrWhiteSpace(TargetId) || string.IsNullOrWhiteSpace(TargetName))
		{
			throw new BusinessException("ShikshaMitra:AttendanceOverrideTargetRequired");
		}

		if (HoursBasedPayPercentage is < 0 or > 100)
		{
			throw new BusinessException("ShikshaMitra:InvalidHoursBasedPayPercentage");
		}

		if (GraceTime < 0 || MaxLateAllowedPerMonth < 0 || MinOvertimeMinutes < 0)
		{
			throw new BusinessException("ShikshaMitra:InvalidAttendanceOverrideRule");
		}

		if (OvertimeEnabled == true && (!OvertimeRate.HasValue || !MinOvertimeMinutes.HasValue))
		{
			throw new BusinessException("ShikshaMitra:OvertimeRateAndMinMinutesRequired");
		}
	}
}
