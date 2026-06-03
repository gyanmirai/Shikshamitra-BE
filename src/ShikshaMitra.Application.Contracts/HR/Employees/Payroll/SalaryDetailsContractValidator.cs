using System;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.Payroll;

public class SalaryDetailsContractValidator : AbstractValidator<SalaryDetailsContract>
{
	private const decimal MaxHours = 744m;
	private const int MaxPunchIdLength = 20;
	private const string EpfNumberPattern = @"^[A-Z]{2}/\d{5}/\d{7}$";
	private const string UanNumberPattern = @"^\d{12}$";

	public SalaryDetailsContractValidator()
	{
		RuleFor(x => x.TotalWorkingHourPerMonth)
			.GreaterThanOrEqualTo(0)
			.WithMessage("Total working hours per month cannot be negative.")
			.LessThanOrEqualTo(MaxHours)
			.WithMessage($"Total working hours per month must not exceed {MaxHours}.")
			.When(x => x.TotalWorkingHourPerMonth.HasValue);

		RuleFor(x => x.PfRuleId)
			.Must(id => id != Guid.Empty)
			.WithMessage("PF Rule ID must be a valid non-empty GUID.")
			.When(x => x.PfRuleId.HasValue);

		RuleFor(x => x.PunchId)
			.MaximumLength(MaxPunchIdLength)
			.WithMessage($"Punch ID must not exceed {MaxPunchIdLength} characters.")
			.Matches(@"^[a-zA-Z0-9\-_]+$")
			.WithMessage("Punch ID may only contain letters, digits, hyphens, and underscores.")
			.When(x => !string.IsNullOrEmpty(x.PunchId));

		RuleFor(x => x.EpfNumber)
			.Matches(EpfNumberPattern)
			.WithMessage("EPF number must follow the format: XX/XXXXX/XXXXXXX (e.g., AP/12345/1234567).")
			.When(x => !string.IsNullOrEmpty(x.EpfNumber));

		RuleFor(x => x.UanNumber)
			.Matches(UanNumberPattern)
			.WithMessage("UAN number must be exactly 12 digits.")
			.When(x => !string.IsNullOrEmpty(x.UanNumber));
	}
}
