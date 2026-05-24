using System;
using System.Collections.Generic;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Policies;

public class AttendancePolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public Guid WorkCalendarPolicyId { get; protected set; }
	public string Name { get; protected set; } = string.Empty;
	public HrPolicyStatus Status { get; protected set; }
	public bool IsDefault { get; protected set; }
	public double FullDayThresholdPercentage { get; protected set; }
	public double HalfDayThresholdPercentage { get; protected set; }
	public double AbsentThresholdPercentage { get; protected set; }
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
	public int Version { get; protected set; }

	protected AttendancePolicy()
	{
	}

	public AttendancePolicy(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid workCalendarPolicyId,
		string name,
		HrPolicyStatus status,
		bool isDefault,
		double fullDayThresholdPercentage,
		double halfDayThresholdPercentage,
		double absentThresholdPercentage,
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
		bool payrollPreviewEnabled) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		WorkCalendarPolicyId = workCalendarPolicyId;
		Version = 0;
		Update(
			name,
			status,
			isDefault,
			fullDayThresholdPercentage,
			halfDayThresholdPercentage,
			absentThresholdPercentage,
			missingPunchBehavior,
			manualCorrectionAllowed,
			graceTime,
			maxLateAllowedPerMonth,
			penaltyType,
			penaltyCountThreshold,
			earlyExitEnabled,
			earlyExitGraceTime,
			overtimeEnabled,
			overtimeRate,
			minOvertimeMinutes,
			holidayWorkBehavior,
			sandwichRuleEnabled,
			payrollPreviewEnabled);
	}

	public void Update(
		string name,
		HrPolicyStatus status,
		bool isDefault,
		double fullDayThresholdPercentage,
		double halfDayThresholdPercentage,
		double absentThresholdPercentage,
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
		bool payrollPreviewEnabled)
	{
		Validate(name, status, isDefault, fullDayThresholdPercentage, halfDayThresholdPercentage, absentThresholdPercentage, graceTime, maxLateAllowedPerMonth, penaltyCountThreshold, earlyExitGraceTime, overtimeEnabled, overtimeRate, minOvertimeMinutes);

		Name = name.Trim();
		Status = status;
		IsDefault = isDefault;
		FullDayThresholdPercentage = fullDayThresholdPercentage;
		HalfDayThresholdPercentage = halfDayThresholdPercentage;
		AbsentThresholdPercentage = absentThresholdPercentage;
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
		Version++;
	}

	public void SetDefault(bool isDefault)
	{
		if (isDefault && Status != HrPolicyStatus.Active)
		{
			throw new BusinessException("ShikshaMitra:DefaultPolicyMustBeActive");
		}

		IsDefault = isDefault;
		Version++;
	}

	public void Archive()
	{
		if (IsDefault)
		{
			throw new BusinessException("ShikshaMitra:DefaultPolicyCannotBeArchived");
		}

		Status = HrPolicyStatus.Archived;
		Version++;
	}

	private static void Validate(
		string name,
		HrPolicyStatus status,
		bool isDefault,
		double fullDayThresholdPercentage,
		double halfDayThresholdPercentage,
		double absentThresholdPercentage,
		int graceTime,
		int maxLateAllowedPerMonth,
		int penaltyCountThreshold,
		int earlyExitGraceTime,
		bool overtimeEnabled,
		AttendanceOvertimeRate? overtimeRate,
		int? minOvertimeMinutes)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new BusinessException("ShikshaMitra:AttendancePolicyNameRequired");
		}

		if (isDefault && status != HrPolicyStatus.Active)
		{
			throw new BusinessException("ShikshaMitra:DefaultPolicyMustBeActive");
		}

		if (fullDayThresholdPercentage is < 0 or > 100 || halfDayThresholdPercentage is < 0 or > 100 || absentThresholdPercentage is < 0 or > 100)
		{
			throw new BusinessException("ShikshaMitra:InvalidAttendanceThreshold");
		}

		if (halfDayThresholdPercentage >= fullDayThresholdPercentage || absentThresholdPercentage > halfDayThresholdPercentage)
		{
			throw new BusinessException("ShikshaMitra:InvalidAttendanceThresholdOrder");
		}

		if (graceTime < 0 || maxLateAllowedPerMonth < 0 || penaltyCountThreshold < 0 || earlyExitGraceTime < 0)
		{
			throw new BusinessException("ShikshaMitra:InvalidAttendancePolicyRule");
		}

		if (overtimeEnabled && (!overtimeRate.HasValue || !minOvertimeMinutes.HasValue || minOvertimeMinutes < 0))
		{
			throw new BusinessException("ShikshaMitra:OvertimeRateAndMinMinutesRequired");
		}
	}
}
