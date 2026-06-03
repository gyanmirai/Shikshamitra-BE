using System;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.Payroll;

public class SalaryComponentContractValidator : AbstractValidator<SalaryComponentContract>
{
	private const int MaxNameLength = 100;
	private const decimal MaxComponentAmount = 99_999_999.99m;

	public SalaryComponentContractValidator(string listName)
	{
		RuleFor(x => x.Name)
			.NotEmpty().WithMessage($"Component name in {listName} is required.")
			.MaximumLength(MaxNameLength).WithMessage($"Component name in {listName} must not exceed {MaxNameLength} characters.");

		RuleFor(x => x.ComponentId)
			.Must(id => !id.HasValue || id.Value != Guid.Empty)
			.WithMessage($"Component ID in {listName} must be a valid non-empty GUID when supplied.");

		RuleFor(x => x.Amount)
			.GreaterThanOrEqualTo(0)
			.WithMessage($"Component amount in {listName} cannot be negative when supplied.")
			.LessThanOrEqualTo(MaxComponentAmount)
			.WithMessage($"Component amount in {listName} must not exceed {MaxComponentAmount}.")
			.When(x => x.Amount.HasValue);
	}
}
