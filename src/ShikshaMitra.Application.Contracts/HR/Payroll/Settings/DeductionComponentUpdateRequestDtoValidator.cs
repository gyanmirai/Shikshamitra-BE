using System;
using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponentUpdateRequestDtoValidator : AbstractValidator<DeductionComponentUpdateRequestDto>
{
	public DeductionComponentUpdateRequestDtoValidator()
	{
		RuleFor(x => x.DeductionComponentName)
			.NotEmpty()
			.Must(x => !string.IsNullOrWhiteSpace(x))
			.WithMessage("Deduction component name must not be empty or whitespace.")
			.MaximumLength(DeductionComponentConsts.MaxNameLength);

		RuleFor(x => x.Description)
			.MaximumLength(DeductionComponentConsts.MaxDescriptionLength)
			.When(x => x.Description != null);

		RuleFor(x => x.DeductionType)
			.NotEmpty()
			.Must(IsValidDeductionType)
			.WithMessage("deductionType must be FIXED or VARIABLE.");

		RuleFor(x => x.CalculationType)
			.NotEmpty()
			.Must(IsValidCalculationType)
			.WithMessage("calculationType must be AMOUNT or PERCENTAGE.");

		RuleFor(x => x.Amount)
			.NotNull()
			.GreaterThanOrEqualTo(0)
			.When(IsFixedAmountCalculation)
			.WithMessage("Amount is required when deductionType is FIXED and calculationType is AMOUNT.");

		RuleFor(x => x.Amount)
			.GreaterThanOrEqualTo(0)
			.When(x => x.Amount.HasValue && !IsFixedAmountCalculation(x))
			.WithMessage("Amount must be a non-negative number.");

		RuleFor(x => x.Percentage)
			.NotNull()
			.InclusiveBetween(0, 100)
			.When(x => IsPercentageCalculation(x.CalculationType))
			.WithMessage("Percentage must be between 0 and 100 when calculationType is PERCENTAGE.");

		RuleFor(x => x.CalculationBase)
			.Must(x => x is "GROSS" or "COMPONENT")
			.When(x => IsPercentageCalculation(x.CalculationType))
			.WithMessage("Calculation base must be GROSS or COMPONENT.");

		RuleFor(x => x.SelectedDeductionComponentId)
			.NotEmpty()
			.When(x => IsPercentageCalculation(x.CalculationType) && x.CalculationBase == "COMPONENT")
			.WithMessage("Select a component for a component-based percentage.");

		RuleFor(x => x.SelectedDeductionComponentId)
			.Null()
			.When(x => IsPercentageCalculation(x.CalculationType) && x.CalculationBase == "GROSS")
			.WithMessage("Gross Salary is a system base and cannot reference a component.");
	}

	private static bool IsValidDeductionType(string? value) =>
		string.Equals(value?.Trim(), "FIXED", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(value?.Trim(), "VARIABLE", StringComparison.OrdinalIgnoreCase);

	private static bool IsValidCalculationType(string? value) =>
		string.Equals(value?.Trim(), "AMOUNT", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(value?.Trim(), "PERCENTAGE", StringComparison.OrdinalIgnoreCase);

	private static bool IsAmountCalculation(string? value) =>
		string.Equals(value?.Trim(), "AMOUNT", StringComparison.OrdinalIgnoreCase);

	private static bool IsPercentageCalculation(string? value) =>
		string.Equals(value?.Trim(), "PERCENTAGE", StringComparison.OrdinalIgnoreCase);

	private static bool IsFixedAmountCalculation(DeductionComponentUpdateRequestDto value) =>
		string.Equals(value.DeductionType?.Trim(), "FIXED", StringComparison.OrdinalIgnoreCase)
		&& IsAmountCalculation(value.CalculationType);
}
