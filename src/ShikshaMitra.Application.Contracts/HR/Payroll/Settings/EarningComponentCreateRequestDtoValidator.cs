using System;
using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentCreateRequestDtoValidator : AbstractValidator<EarningComponentCreateRequestDto>
{
	public EarningComponentCreateRequestDtoValidator()
	{
		RuleFor(x => x.EarningComponentName)
			.NotEmpty()
			.Must(x => !string.IsNullOrWhiteSpace(x))
			.WithMessage("Earning component name must not be empty or whitespace.")
			.MaximumLength(EarningComponentConsts.MaxNameLength);

		RuleFor(x => x.Description)
			.MaximumLength(EarningComponentConsts.MaxDescriptionLength)
			.When(x => x.Description != null);

		RuleFor(x => x.EarningType)
			.NotEmpty()
			.Must(IsValidEarningType)
			.WithMessage("earningType must be FIXED or VARIABLE.");

		RuleFor(x => x.CalculationType)
			.NotEmpty()
			.Must(IsValidCalculationType)
			.WithMessage("calculationType must be AMOUNT or PERCENTAGE.");

		RuleFor(x => x.Amount)
			.NotNull()
			.GreaterThanOrEqualTo(0)
			.When(x => IsAmountCalculation(x.CalculationType))
			.WithMessage("Amount is required when calculationType is AMOUNT.");

		RuleFor(x => x.Percentage)
			.NotNull()
			.InclusiveBetween(0, 100)
			.When(x => IsPercentageCalculation(x.CalculationType))
			.WithMessage("Percentage must be between 0 and 100 when calculationType is PERCENTAGE.");

		RuleFor(x => x.CalculationBase)
			.Must(x => x is "GROSS" or "COMPONENT")
			.When(x => IsPercentageCalculation(x.CalculationType))
			.WithMessage("Calculation base must be GROSS or COMPONENT.");

		RuleFor(x => x.SelectedEarningComponentId)
			.NotEmpty()
			.When(x => IsPercentageCalculation(x.CalculationType) && x.CalculationBase == "COMPONENT")
			.WithMessage("Select a component for a component-based percentage.");

		RuleFor(x => x.SelectedEarningComponentId)
			.Null()
			.When(x => IsPercentageCalculation(x.CalculationType) && x.CalculationBase == "GROSS")
			.WithMessage("Gross Salary is a system base and cannot reference a component.");
	}

	private static bool IsValidEarningType(string? value) =>
		string.Equals(value?.Trim(), "FIXED", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(value?.Trim(), "VARIABLE", StringComparison.OrdinalIgnoreCase);

	private static bool IsValidCalculationType(string? value) =>
		string.Equals(value?.Trim(), "AMOUNT", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(value?.Trim(), "PERCENTAGE", StringComparison.OrdinalIgnoreCase);

	private static bool IsAmountCalculation(string? value) =>
		string.Equals(value?.Trim(), "AMOUNT", StringComparison.OrdinalIgnoreCase);

	private static bool IsPercentageCalculation(string? value) =>
		string.Equals(value?.Trim(), "PERCENTAGE", StringComparison.OrdinalIgnoreCase);
}
