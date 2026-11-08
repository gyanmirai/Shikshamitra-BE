using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

public class AttendanceRegisterAdjustRequestValidator : AbstractValidator<AttendanceRegisterAdjustRequest>
{
	public AttendanceRegisterAdjustRequestValidator()
	{
		RuleFor(x => x)
			.Must(x => x.AttendanceId.HasValue || (x.EmployeeId.HasValue && !string.IsNullOrWhiteSpace(x.AttendanceDate)))
			.WithMessage("AttendanceId or EmployeeId with AttendanceDate is required.");
		RuleFor(x => x.StatusWire).NotEmpty().MaximumLength(20);
		RuleFor(x => x.AttendanceDate).MaximumLength(40);
		RuleFor(x => x.InTime).MaximumLength(40);
		RuleFor(x => x.OutTime).MaximumLength(40);
		RuleFor(x => x.Remarks).MaximumLength(500);
		RuleFor(x => x.Reason).MaximumLength(500);
	}
}

public class AttendanceRegisterBulkAdjustRequestValidator : AbstractValidator<AttendanceRegisterBulkAdjustRequest>
{
	public AttendanceRegisterBulkAdjustRequestValidator()
	{
		RuleFor(x => x.AttendanceIds).NotNull();
		RuleFor(x => x.EmployeeDates).NotNull();
		RuleFor(x => x)
			.Must(x => x.AttendanceIds is { Count: > 0 } || x.EmployeeDates is { Count: > 0 })
			.WithMessage("At least one attendance ID or employee date is required.");
		RuleForEach(x => x.AttendanceIds).NotEmpty();
		RuleForEach(x => x.EmployeeDates).NotNull().ChildRules(employeeDate =>
		{
			employeeDate.RuleFor(x => x.EmployeeId).NotEmpty();
			employeeDate.RuleFor(x => x.AttendanceDate).NotEmpty().MaximumLength(40);
		});
		RuleFor(x => x.StatusWire).NotEmpty().MaximumLength(20);
		RuleFor(x => x.InTime).MaximumLength(40);
		RuleFor(x => x.OutTime).MaximumLength(40);
		RuleFor(x => x.Remarks).MaximumLength(500);
		RuleFor(x => x.Reason).MaximumLength(500);
	}
}
