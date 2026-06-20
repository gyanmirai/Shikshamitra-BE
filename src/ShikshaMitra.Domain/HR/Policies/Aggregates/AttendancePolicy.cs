using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
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
	public AttendanceCaptureMode CaptureMode { get; protected set; } = AttendanceCaptureMode.Integrated;
	public double FullDayThresholdPercentage { get; protected set; }
	public double HalfDayThresholdPercentage { get; protected set; }
	public double AbsentThresholdPercentage { get; protected set; }
	public AttendanceMissingPunchBehavior MissingPunchBehavior { get; protected set; }
	public bool ManualCorrectionAllowed { get; protected set; }
	public int GraceTime { get; protected set; }
	public int MaxLateAllowedPerMonth { get; protected set; }
	public AttendancePenaltyType PenaltyType { get; protected set; }
	public int PenaltyCountThreshold { get; protected set; }
	public decimal? PenaltyDeductionAmount { get; protected set; }
	public bool EarlyExitEnabled { get; protected set; }
	public int EarlyExitGraceTime { get; protected set; }
	public bool OvertimeEnabled { get; protected set; }
	public AttendanceOvertimeRate? OvertimeRate { get; protected set; }
	public int? MinOvertimeMinutes { get; protected set; }
	public AttendanceHolidayWorkBehavior HolidayWorkBehavior { get; protected set; }
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
		AttendanceCaptureMode captureMode,
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
		bool payrollPreviewEnabled,
		decimal? penaltyDeductionAmount = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		WorkCalendarPolicyId = workCalendarPolicyId;
		Version = 0;
		Update(
			workCalendarPolicyId,
			name,
			status,
			isDefault,
			captureMode,
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
			payrollPreviewEnabled,
			penaltyDeductionAmount);
	}

	public void Update(
		Guid workCalendarPolicyId,
		string name,
		HrPolicyStatus status,
		bool isDefault,
		AttendanceCaptureMode captureMode,
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
		bool payrollPreviewEnabled,
		decimal? penaltyDeductionAmount = null)
	{
		if (AcademicYearId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.AcademicYearRequired);
		}

		if (workCalendarPolicyId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarPolicyRequired);
		}

		if (status == HrPolicyStatus.Archived)
		{
			throw new BusinessException(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		}

		AttendancePolicyRuleValidator.Validate(
			name,
			status,
			isDefault,
			fullDayThresholdPercentage,
			halfDayThresholdPercentage,
			absentThresholdPercentage,
			graceTime,
			maxLateAllowedPerMonth,
			penaltyType,
			penaltyCountThreshold,
			penaltyDeductionAmount,
			earlyExitGraceTime,
			overtimeEnabled,
			overtimeRate,
			minOvertimeMinutes);

		WorkCalendarPolicyId = workCalendarPolicyId;
		Name = name.Trim();
		Status = status;
		IsDefault = isDefault;
		CaptureMode = captureMode;
		FullDayThresholdPercentage = fullDayThresholdPercentage;
		HalfDayThresholdPercentage = halfDayThresholdPercentage;
		AbsentThresholdPercentage = absentThresholdPercentage;
		MissingPunchBehavior = missingPunchBehavior;
		ManualCorrectionAllowed = manualCorrectionAllowed;
		GraceTime = graceTime;
		MaxLateAllowedPerMonth = maxLateAllowedPerMonth;
		PenaltyType = penaltyType;
		PenaltyCountThreshold = penaltyCountThreshold;
		PenaltyDeductionAmount = penaltyType == AttendancePenaltyType.SalaryDeduction
			? penaltyDeductionAmount
			: null;
		EarlyExitEnabled = earlyExitEnabled;
		EarlyExitGraceTime = earlyExitGraceTime;
		OvertimeEnabled = overtimeEnabled;
		OvertimeRate = overtimeEnabled ? overtimeRate : null;
		MinOvertimeMinutes = overtimeEnabled ? minOvertimeMinutes : null;
		HolidayWorkBehavior = holidayWorkBehavior;
		PayrollPreviewEnabled = payrollPreviewEnabled;
		Version++;
	}

	public void SetDefault(bool isDefault)
	{
		if (isDefault && Status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.DefaultPolicyMustBeActive);
		}

		IsDefault = isDefault;
		Version++;
	}

	public void Archive()
	{
		if (IsDefault)
		{
			throw new BusinessException(HRErrorCodes.DefaultPolicyCannotBeArchived);
		}

		Status = HrPolicyStatus.Archived;
		Version++;
	}

	public void Unarchive()
	{
		Status = HrPolicyStatus.Draft;
		IsDefault = false;
		Version++;
	}
}
