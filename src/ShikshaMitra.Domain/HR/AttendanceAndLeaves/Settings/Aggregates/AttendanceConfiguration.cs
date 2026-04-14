using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendanceConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public double HoursBasedPayPercentage { get; protected set; }
	public int GraceTime { get; protected set; }
	public int MaxLateAllowedPerMonth { get; protected set; }
	public AttendancePenaltyType PenaltyType { get; protected set; }
	public bool OvertimeEnabled { get; protected set; }
	public AttendanceOvertimeRate? OvertimeRate { get; protected set; }
	public int? MinOvertimeMinutes { get; protected set; }

	protected AttendanceConfiguration()
	{
	}

	public AttendanceConfiguration(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		double hoursBasedPayPercentage,
		int graceTime,
		int maxLateAllowedPerMonth,
		AttendancePenaltyType penaltyType,
		bool overtimeEnabled,
		AttendanceOvertimeRate? overtimeRate,
		int? minOvertimeMinutes) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Update(hoursBasedPayPercentage, graceTime, maxLateAllowedPerMonth, penaltyType, overtimeEnabled, overtimeRate, minOvertimeMinutes);
	}

	public void Update(
		double hoursBasedPayPercentage,
		int graceTime,
		int maxLateAllowedPerMonth,
		AttendancePenaltyType penaltyType,
		bool overtimeEnabled,
		AttendanceOvertimeRate? overtimeRate,
		int? minOvertimeMinutes)
	{
		if (hoursBasedPayPercentage < 0 || hoursBasedPayPercentage > 100)
		{
			throw new BusinessException("ShikshaMitra:InvalidHoursBasedPayPercentage");
		}

		if (graceTime < 0)
		{
			throw new BusinessException("ShikshaMitra:InvalidGraceTime");
		}

		if (maxLateAllowedPerMonth < 0)
		{
			throw new BusinessException("ShikshaMitra:InvalidMaxLateAllowedPerMonth");
		}

		if (overtimeEnabled && (!overtimeRate.HasValue || !minOvertimeMinutes.HasValue))
		{
			throw new BusinessException("ShikshaMitra:OvertimeRateAndMinMinutesRequired");
		}

		HoursBasedPayPercentage = hoursBasedPayPercentage;
		GraceTime = graceTime;
		MaxLateAllowedPerMonth = maxLateAllowedPerMonth;
		PenaltyType = penaltyType;
		OvertimeEnabled = overtimeEnabled;
		OvertimeRate = overtimeEnabled ? overtimeRate : null;
		MinOvertimeMinutes = overtimeEnabled ? minOvertimeMinutes : null;
	}
}
