using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendanceConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public string PolicyName { get; protected set; } = "Default Attendance Policy";
	public AttendanceConfigurationStatus Status { get; protected set; }
	public DateTime? EffectiveFrom { get; protected set; }
	public DateTime? EffectiveTo { get; protected set; }
	public AttendanceWeeklyOffPattern WeeklyOffPattern { get; protected set; }
	public string? CustomWeeklyOffs { get; protected set; }
	public double HoursBasedPayPercentage { get; protected set; }
	public double HalfDayPayPercentage { get; protected set; }
	public double AbsentPayPercentage { get; protected set; }
	public AttendanceMissingPunchBehavior MissingPunchBehavior { get; protected set; }
	public bool ManualCorrectionAllowed { get; protected set; }
	public int GraceTime { get; protected set; }
	public int MaxLateAllowedPerMonth { get; protected set; }
	public AttendancePenaltyType PenaltyType { get; protected set; }
	public int PenaltyCountThreshold { get; protected set; }
	public bool EarlyExitEnabled { get; protected set; }
	public int EarlyExitGraceTime { get; protected set; }
	public bool OvertimeEnabled { get; protected set; }
	public AttendanceOvertimeRate? OvertimeRate { get; protected set; }
	public int? MinOvertimeMinutes { get; protected set; }
	public AttendanceHolidayWorkBehavior HolidayWorkBehavior { get; protected set; }
	public bool SandwichRuleEnabled { get; protected set; }
	public bool PayrollPreviewEnabled { get; protected set; }
	public AttendancePayrollIntegrationMode PayrollIntegrationMode { get; protected set; }
	public bool RequirePublishedAttendanceForPayrollLock { get; protected set; }
	public ICollection<AttendancePolicyOverride> Overrides { get; protected set; } = new List<AttendancePolicyOverride>();

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
		Update(
			"Default Attendance Policy",
			AttendanceConfigurationStatus.Draft,
			null,
			null,
			AttendanceWeeklyOffPattern.SaturdaySunday,
			null,
			hoursBasedPayPercentage,
			50,
			0,
			AttendanceMissingPunchBehavior.RequiresReview,
			true,
			graceTime,
			maxLateAllowedPerMonth,
			penaltyType,
			maxLateAllowedPerMonth,
			false,
			0,
			overtimeEnabled,
			overtimeRate,
			minOvertimeMinutes,
			AttendanceHolidayWorkBehavior.CompOff,
			false,
			true,
			AttendancePayrollIntegrationMode.Advisory,
			false);
	}

	public void Update(
		string policyName,
		AttendanceConfigurationStatus status,
		DateTime? effectiveFrom,
		DateTime? effectiveTo,
		AttendanceWeeklyOffPattern weeklyOffPattern,
		string? customWeeklyOffs,
		double hoursBasedPayPercentage,
		double halfDayPayPercentage,
		double absentPayPercentage,
		AttendanceMissingPunchBehavior missingPunchBehavior,
		bool manualCorrectionAllowed,
		int graceTime,
		int maxLateAllowedPerMonth,
		AttendancePenaltyType penaltyType,
		int penaltyCountThreshold,
		bool earlyExitEnabled,
		int earlyExitGraceTime,
		bool overtimeEnabled,
		AttendanceOvertimeRate? overtimeRate,
		int? minOvertimeMinutes,
		AttendanceHolidayWorkBehavior holidayWorkBehavior,
		bool sandwichRuleEnabled,
		bool payrollPreviewEnabled,
		AttendancePayrollIntegrationMode payrollIntegrationMode,
		bool requirePublishedAttendanceForPayrollLock)
	{
		if (string.IsNullOrWhiteSpace(policyName))
		{
			throw new BusinessException("ShikshaMitra:AttendancePolicyNameRequired");
		}

		if (status == AttendanceConfigurationStatus.Active && !effectiveFrom.HasValue)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEffectiveFromRequired");
		}

		if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value.Date < effectiveFrom.Value.Date)
		{
			throw new BusinessException("ShikshaMitra:AttendanceEffectiveRangeInvalid");
		}

		if (hoursBasedPayPercentage < 0 || hoursBasedPayPercentage > 100)
		{
			throw new BusinessException("ShikshaMitra:InvalidHoursBasedPayPercentage");
		}

		if (halfDayPayPercentage < 0 || halfDayPayPercentage > 100 || absentPayPercentage < 0 || absentPayPercentage > 100)
		{
			throw new BusinessException("ShikshaMitra:InvalidAttendanceThreshold");
		}

		if (halfDayPayPercentage >= hoursBasedPayPercentage)
		{
			throw new BusinessException("ShikshaMitra:HalfDayThresholdMustBeLessThanFullDay");
		}

		if (absentPayPercentage > halfDayPayPercentage)
		{
			throw new BusinessException("ShikshaMitra:AbsentThresholdMustBeLessThanHalfDay");
		}

		if (graceTime < 0)
		{
			throw new BusinessException("ShikshaMitra:InvalidGraceTime");
		}

		if (maxLateAllowedPerMonth < 0 || penaltyCountThreshold < 0 || earlyExitGraceTime < 0)
		{
			throw new BusinessException("ShikshaMitra:InvalidMaxLateAllowedPerMonth");
		}

		if (overtimeEnabled && (!overtimeRate.HasValue || !minOvertimeMinutes.HasValue))
		{
			throw new BusinessException("ShikshaMitra:OvertimeRateAndMinMinutesRequired");
		}

		if (overtimeEnabled && minOvertimeMinutes < 0)
		{
			throw new BusinessException("ShikshaMitra:InvalidMinOvertimeMinutes");
		}

		PolicyName = policyName.Trim();
		Status = status;
		EffectiveFrom = effectiveFrom;
		EffectiveTo = effectiveTo;
		WeeklyOffPattern = weeklyOffPattern;
		CustomWeeklyOffs = customWeeklyOffs;
		HoursBasedPayPercentage = hoursBasedPayPercentage;
		HalfDayPayPercentage = halfDayPayPercentage;
		AbsentPayPercentage = absentPayPercentage;
		MissingPunchBehavior = missingPunchBehavior;
		ManualCorrectionAllowed = manualCorrectionAllowed;
		GraceTime = graceTime;
		MaxLateAllowedPerMonth = maxLateAllowedPerMonth;
		PenaltyType = penaltyType;
		PenaltyCountThreshold = penaltyCountThreshold;
		EarlyExitEnabled = earlyExitEnabled;
		EarlyExitGraceTime = earlyExitGraceTime;
		OvertimeEnabled = overtimeEnabled;
		OvertimeRate = overtimeEnabled ? overtimeRate : null;
		MinOvertimeMinutes = overtimeEnabled ? minOvertimeMinutes : null;
		HolidayWorkBehavior = holidayWorkBehavior;
		SandwichRuleEnabled = sandwichRuleEnabled;
		PayrollPreviewEnabled = payrollPreviewEnabled;
		PayrollIntegrationMode = payrollIntegrationMode;
		RequirePublishedAttendanceForPayrollLock = requirePublishedAttendanceForPayrollLock;
	}

	public void ReplaceOverrides(IEnumerable<AttendancePolicyOverride> overrides)
	{
		var nextOverrides = overrides.ToList();
		var duplicate = nextOverrides
			.Where(x => x.IsActive)
			.GroupBy(x => new { x.TargetType, x.TargetId })
			.FirstOrDefault(x => x.Count() > 1);

		if (duplicate != null)
		{
			throw new BusinessException("ShikshaMitra:DuplicateAttendancePolicyOverride");
		}

		Overrides.Clear();
		foreach (var item in nextOverrides)
		{
			Overrides.Add(item);
		}
	}
}
