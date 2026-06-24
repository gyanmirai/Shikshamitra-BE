using System;
using FluentValidation;
using static ShikshaMitra.HR.Policies.HrPolicyRequestValidation;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class AttendancePolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public string CaptureMode { get; set; } = "integrated";
	public double FullDayThresholdPercentage { get; set; }
	public double HalfDayThresholdPercentage { get; set; }
	public double AbsentThresholdPercentage { get; set; }
	public string MissingPunchBehavior { get; set; } = "requires_review";
	public bool ManualCorrectionAllowed { get; set; }
	public int GraceTime { get; set; }
	public int MaxLateAllowedPerMonth { get; set; }
	public string PenaltyType { get; set; } = "none";
	public int PenaltyCountThreshold { get; set; }
	public decimal? PenaltyDeductionAmount { get; set; }
	public bool EarlyExitEnabled { get; set; }
	public int EarlyExitGraceTime { get; set; }
	public bool OvertimeEnabled { get; set; }
	public string? OvertimeRate { get; set; }
	public int? MinOvertimeMinutes { get; set; }
	public string HolidayWorkBehavior { get; set; } = "comp_off";
	public bool PayrollPreviewEnabled { get; set; }
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdateAttendancePolicyDto
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public string CaptureMode { get; set; } = "integrated";
	public double FullDayThresholdPercentage { get; set; } = 75;
	public double HalfDayThresholdPercentage { get; set; } = 50;
	public double AbsentThresholdPercentage { get; set; }
	public string MissingPunchBehavior { get; set; } = "requires_review";
	public bool ManualCorrectionAllowed { get; set; } = true;
	public int GraceTime { get; set; }
	public int MaxLateAllowedPerMonth { get; set; }
	public string PenaltyType { get; set; } = "none";
	public int PenaltyCountThreshold { get; set; }
	public decimal? PenaltyDeductionAmount { get; set; }
	public bool EarlyExitEnabled { get; set; }
	public int EarlyExitGraceTime { get; set; }
	public bool OvertimeEnabled { get; set; }
	public string? OvertimeRate { get; set; }
	public int? MinOvertimeMinutes { get; set; }
	public string HolidayWorkBehavior { get; set; } = "comp_off";
	public bool PayrollPreviewEnabled { get; set; } = true;
}

public class CreateUpdateAttendancePolicyDtoValidator : AbstractValidator<CreateUpdateAttendancePolicyDto>
{
	public CreateUpdateAttendancePolicyDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.WorkCalendarPolicyId).NotEmpty();
		RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Status).Must(BeEditablePolicyStatus).WithMessage("Status must be draft or active. Use the archive action to archive a policy.");
		RuleFor(x => x.CaptureMode)
			.Must(BeKnownAttendanceCaptureMode)
			.WithMessage("Capture mode must be integrated, manual_status, or manual_with_time.");
		RuleFor(x => x.MissingPunchBehavior)
			.Must(BeKnownMissingPunchBehavior)
			.WithMessage("Missing punch behavior must be requires_review, mark_absent, or mark_half_day.");
		RuleFor(x => x.PenaltyType)
			.Must(BeKnownAttendancePenaltyType)
			.WithMessage("Penalty type must be none, warning, salary_deduction, deduct_half_day, or deduct_full_day.");
		RuleFor(x => x.OvertimeRate)
			.Must(BeKnownAttendanceOvertimeRate)
			.WithMessage("Overtime rate must be 1.5x, 2.0x, or flat.");
		RuleFor(x => x.HolidayWorkBehavior)
			.Must(BeKnownAttendanceHolidayWorkBehavior)
			.WithMessage("Holiday work behavior must be comp_off, paid, both, ot_payout, or no_benefit.");

		RuleFor(x => x)
			.Must(x => !x.IsDefault || IsActive(x.Status))
			.WithMessage("Default attendance policies must be active.");

		RuleFor(x => x.FullDayThresholdPercentage)
			.InclusiveBetween(0, 100);

		RuleFor(x => x.HalfDayThresholdPercentage)
			.InclusiveBetween(0, 100);

		RuleFor(x => x.AbsentThresholdPercentage)
			.InclusiveBetween(0, 100);

		RuleFor(x => x)
			.Must(x => x.HalfDayThresholdPercentage < x.FullDayThresholdPercentage &&
				x.AbsentThresholdPercentage <= x.HalfDayThresholdPercentage)
			.WithMessage("Attendance thresholds must be ordered as absent <= half-day < full-day.");

		RuleFor(x => x.GraceTime)
			.InclusiveBetween(0, 240);

		RuleFor(x => x.MaxLateAllowedPerMonth)
			.InclusiveBetween(0, 31);

		RuleFor(x => x.PenaltyCountThreshold)
			.InclusiveBetween(0, 31);

		RuleFor(x => x.PenaltyDeductionAmount)
			.NotNull()
			.When(x => string.Equals(x.PenaltyType, "salary_deduction", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Penalty deduction amount is required when penalty type is salary_deduction.");

		RuleFor(x => x.PenaltyDeductionAmount)
			.GreaterThan(0)
			.When(x => string.Equals(x.PenaltyType, "salary_deduction", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Penalty deduction amount must be greater than 0 when penalty type is salary_deduction.");

		RuleFor(x => x.PenaltyDeductionAmount)
			.Must(x => !x.HasValue || x.Value <= 1_000_000m)
			.WithMessage("Penalty deduction amount must be 1,000,000 or less.");

		RuleFor(x => x.EarlyExitGraceTime)
			.InclusiveBetween(0, 240);

		RuleFor(x => x.MinOvertimeMinutes)
			.InclusiveBetween(0, 1440)
			.When(x => x.MinOvertimeMinutes.HasValue);

		RuleFor(x => x.OvertimeRate)
			.NotEmpty()
			.When(x => x.OvertimeEnabled)
			.WithMessage("Overtime rate is required when overtime is enabled.");

		RuleFor(x => x.MinOvertimeMinutes)
			.NotNull()
			.When(x => x.OvertimeEnabled)
			.WithMessage("Minimum overtime minutes are required when overtime is enabled.");
	}
}
