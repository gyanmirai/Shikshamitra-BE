using System;
using FluentValidation;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class AttendancePolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public double FullDayThresholdPercentage { get; set; }
	public double HalfDayThresholdPercentage { get; set; }
	public double AbsentThresholdPercentage { get; set; }
	public string MissingPunchBehavior { get; set; } = "requires_review";
	public bool ManualCorrectionAllowed { get; set; }
	public int GraceTime { get; set; }
	public int MaxLateAllowedPerMonth { get; set; }
	public string PenaltyType { get; set; } = "none";
	public int PenaltyCountThreshold { get; set; }
	public bool EarlyExitEnabled { get; set; }
	public int EarlyExitGraceTime { get; set; }
	public bool OvertimeEnabled { get; set; }
	public string? OvertimeRate { get; set; }
	public int? MinOvertimeMinutes { get; set; }
	public string HolidayWorkBehavior { get; set; } = "comp_off";
	public bool SandwichRuleEnabled { get; set; }
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
	public double FullDayThresholdPercentage { get; set; } = 75;
	public double HalfDayThresholdPercentage { get; set; } = 50;
	public double AbsentThresholdPercentage { get; set; }
	public string MissingPunchBehavior { get; set; } = "requires_review";
	public bool ManualCorrectionAllowed { get; set; } = true;
	public int GraceTime { get; set; }
	public int MaxLateAllowedPerMonth { get; set; }
	public string PenaltyType { get; set; } = "none";
	public int PenaltyCountThreshold { get; set; }
	public bool EarlyExitEnabled { get; set; }
	public int EarlyExitGraceTime { get; set; }
	public bool OvertimeEnabled { get; set; }
	public string? OvertimeRate { get; set; }
	public int? MinOvertimeMinutes { get; set; }
	public string HolidayWorkBehavior { get; set; } = "comp_off";
	public bool SandwichRuleEnabled { get; set; }
	public bool PayrollPreviewEnabled { get; set; } = true;
}

public class CreateUpdateAttendancePolicyDtoValidator : AbstractValidator<CreateUpdateAttendancePolicyDto>
{
	public CreateUpdateAttendancePolicyDtoValidator()
	{
		RuleFor(x => x.GraceTime)
			.InclusiveBetween(0, 240);

		RuleFor(x => x.MaxLateAllowedPerMonth)
			.InclusiveBetween(0, 31);

		RuleFor(x => x.PenaltyCountThreshold)
			.InclusiveBetween(0, 31);

		RuleFor(x => x.EarlyExitGraceTime)
			.InclusiveBetween(0, 240);

		RuleFor(x => x.MinOvertimeMinutes)
			.InclusiveBetween(0, 1440)
			.When(x => x.MinOvertimeMinutes.HasValue);
	}
}
