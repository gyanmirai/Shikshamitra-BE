using FluentValidation;
using System;
using System.Linq;
using static ShikshaMitra.HR.Policies.HrPolicyRequestValidation;

namespace ShikshaMitra.HR.Policies;

public class CreateUpdateWorkCalendarPolicyDtoValidator : AbstractValidator<CreateUpdateWorkCalendarPolicyDto>
{
	public CreateUpdateWorkCalendarPolicyDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Status).Must(BeEditablePolicyStatus).WithMessage("Status must be draft or active. Use the archive action to archive a policy.");
		RuleFor(x => x.WeeklyOffPattern)
			.Must(BeKnownWeeklyOffPattern)
			.WithMessage("Weekly off pattern must be saturday_sunday, sunday, or custom.");
		RuleFor(x => x.HolidaySyncMode)
			.Must(BeKnownHolidaySyncMode)
			.WithMessage("Holiday sync mode must be linked_with_overrides, snapshot, or independent.");
		RuleFor(x => x.EffectiveFrom)
			.NotNull()
			.When(x => IsActive(x.Status))
			.WithMessage("Effective from is required for active policies.");
		RuleFor(x => x)
			.Must(x => !x.EffectiveFrom.HasValue || !x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= x.EffectiveFrom.Value.Date)
			.WithMessage("Effective to must be on or after effective from.");
		RuleFor(x => x.CustomWeeklyOffs)
			.Must(days => days != null && days.All(day => day is >= 0 and <= 6))
			.WithMessage("Custom weekly offs must be weekday values between 0 and 6.");
		RuleFor(x => x.CustomWeeklyOffs)
			.Must((input, days) => !IsCustomWeeklyOffPattern(input.WeeklyOffPattern) || (days?.Count ?? 0) > 0)
			.WithMessage("Select at least one custom weekly off day when weekly off pattern is custom.");
	}
}

public class CreateUpdatePayrollPolicyDtoValidator : AbstractValidator<CreateUpdatePayrollPolicyDto>
{
	public CreateUpdatePayrollPolicyDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.WorkCalendarPolicyId).NotEmpty();
		RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Status).Must(BeEditablePolicyStatus).WithMessage("Status must be draft or active. Use the archive action to archive a policy.");
		RuleFor(x => x.DayCalculationType)
			.Must(BeKnownDayCalculationType)
			.WithMessage("Day calculation type must be working-days, total-month-days, or manual-days.");
		RuleFor(x => x.SaturdayWorkPolicy)
			.Must(BeKnownSaturdayWorkPolicy)
			.WithMessage("Saturday work policy must be full-day, half-day, or off.");
		RuleFor(x => x)
			.Must(x => !x.IsDefault || IsActive(x.Status))
			.WithMessage("Default payroll policies must be active.");
		RuleFor(x => x.ManualDays)
			.InclusiveBetween(1, 31)
			.When(x => x.ManualDays.HasValue);
		RuleForEach(x => x.ManualMonthDays).ChildRules(month =>
		{
			month.RuleFor(x => x.Year).InclusiveBetween(1900, 2100);
			month.RuleFor(x => x.Month).InclusiveBetween(1, 12);
			month.RuleFor(x => x.ManualDays).InclusiveBetween(1, 31);
		});
		RuleFor(x => x.ManualMonthDays)
			.Must(months => months != null && months.Select(x => new { x.Year, x.Month }).Distinct().Count() == months.Count)
			.WithMessage("Manual month days cannot contain duplicate year/month entries.");
		RuleFor(x => x)
			.Must(x => !IsManualDays(x.DayCalculationType) || x.ManualDays.HasValue || (x.ManualMonthDays?.Count ?? 0) > 0)
			.WithMessage("Manual days or month-specific manual days are required when day calculation type is manual-days.");
		RuleFor(x => x.HolidayOverrides)
			.Must(overrides => overrides != null && overrides.Select(x => x.Date.Date).Distinct().Count() == overrides.Count)
			.WithMessage("Holiday overrides cannot contain duplicate dates.");
		RuleForEach(x => x.HolidayOverrides).ChildRules(holiday =>
		{
			holiday.RuleFor(x => x.Date).Must(date => date != default).WithMessage("Holiday override date is required.");
			holiday.RuleFor(x => x.HolidayName).NotEmpty().MaximumLength(200);
		});
	}
}

public class CreateUpdateHrPolicyAssignmentDtoValidator : AbstractValidator<CreateUpdateHrPolicyAssignmentDto>
{
	public CreateUpdateHrPolicyAssignmentDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.TargetType)
			.NotEmpty()
			.Must(BeKnownTargetType)
			.WithMessage("Target type must be employee or group.");
		RuleFor(x => x.TargetKey).NotEmpty().MaximumLength(100);
		RuleFor(x => x.TargetDisplayName).MaximumLength(256);
		RuleFor(x => x.AttendancePolicyId).NotEmpty();
		RuleFor(x => x.PayrollPolicyId).NotEmpty();
		RuleFor(x => x.EffectiveFrom).Must(date => date != default).WithMessage("Effective from is required.");
		RuleFor(x => x)
			.Must(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= x.EffectiveFrom.Date)
			.WithMessage("Effective to must be on or after effective from.");
		RuleFor(x => x.Priority).InclusiveBetween(-1000, 1000);
		RuleFor(x => x.AssignmentReason).MaximumLength(256);
	}
}

public class UpsertEmployeePolicyOverrideDtoValidator : AbstractValidator<UpsertEmployeePolicyOverrideDto>
{
	public UpsertEmployeePolicyOverrideDtoValidator()
	{
		RuleFor(x => x.EmployeeId).NotEmpty();
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.AssignmentId)
			.Must(id => !id.HasValue || id.Value != Guid.Empty)
			.WithMessage("Assignment id must not be empty when provided.");
		RuleFor(x => x.AttendancePolicyId).NotEmpty();
		RuleFor(x => x.PayrollPolicyId).NotEmpty();
		RuleFor(x => x.EffectiveFrom).Must(date => date != default).WithMessage("Effective from is required.");
		RuleFor(x => x)
			.Must(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= x.EffectiveFrom.Date)
			.WithMessage("Effective to must be on or after effective from.");
		RuleFor(x => x.AssignmentReason).MaximumLength(256);
	}
}

public class DisableEmployeePolicyOverrideDtoValidator : AbstractValidator<DisableEmployeePolicyOverrideDto>
{
	public DisableEmployeePolicyOverrideDtoValidator()
	{
		RuleFor(x => x.EmployeeId).NotEmpty();
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.AssignmentId)
			.Must(id => !id.HasValue || id.Value != Guid.Empty)
			.WithMessage("Assignment id must not be empty when provided.");
		RuleFor(x => x.EffectiveDate).Must(date => date != default).WithMessage("Effective date is required.");
		RuleFor(x => x.AssignmentReason).MaximumLength(256);
	}
}

public class HrPolicyCloneRequestDtoValidator : AbstractValidator<HrPolicyCloneRequestDto>
{
	public HrPolicyCloneRequestDtoValidator()
	{
		RuleFor(x => x.Name).MaximumLength(200);
	}
}

public class HrPolicyImpactPreviewRequestDtoValidator : AbstractValidator<HrPolicyImpactPreviewRequestDto>
{
	public HrPolicyImpactPreviewRequestDtoValidator()
	{
		RuleFor(x => x.PolicyType)
			.NotEmpty()
			.Must(BeKnownPolicyType)
			.WithMessage("Policy type must be work_calendar, payroll, or attendance.");
		RuleFor(x => x.Action).NotEmpty().MaximumLength(50);
		RuleFor(x => x.WorkCalendarPolicy)
			.SetValidator(new CreateUpdateWorkCalendarPolicyDtoValidator()!)
			.When(x => x.WorkCalendarPolicy != null);
		RuleFor(x => x.PayrollPolicy)
			.SetValidator(new CreateUpdatePayrollPolicyDtoValidator()!)
			.When(x => x.PayrollPolicy != null);
		RuleFor(x => x.AttendancePolicy)
			.SetValidator(new CreateUpdateAttendancePolicyDtoValidator()!)
			.When(x => x.AttendancePolicy != null);
	}
}

public class HrPolicyAssignmentImpactPreviewRequestDtoValidator : AbstractValidator<HrPolicyAssignmentImpactPreviewRequestDto>
{
	public HrPolicyAssignmentImpactPreviewRequestDtoValidator()
	{
		RuleFor(x => x.Action).NotEmpty().MaximumLength(50);
		RuleFor(x => x.Assignment).NotNull().SetValidator(new CreateUpdateHrPolicyAssignmentDtoValidator());
	}
}

public class PolicyAssignmentPreviewRequestDtoValidator : AbstractValidator<PolicyAssignmentPreviewRequestDto>
{
	public PolicyAssignmentPreviewRequestDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.Date).Must(date => date != default).WithMessage("Preview date is required.");
		RuleFor(x => x)
			.Must(x => x.EmployeeId.HasValue || x.GroupId.HasValue)
			.WithMessage("Employee or group is required for policy assignment preview.");
	}
}

public class HrPolicyRestoreDraftRequestDtoValidator : AbstractValidator<HrPolicyRestoreDraftRequestDto>
{
	public HrPolicyRestoreDraftRequestDtoValidator()
	{
		RuleFor(x => x.Name).MaximumLength(200);
		RuleFor(x => x.ChangeReason).MaximumLength(500);
	}
}
