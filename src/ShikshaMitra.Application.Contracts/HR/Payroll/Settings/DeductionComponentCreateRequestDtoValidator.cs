using System;
using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponentCreateRequestDtoValidator : AbstractValidator<DeductionComponentCreateRequestDto>
{
	public DeductionComponentCreateRequestDtoValidator()
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
			.When(x => IsAmountCalculation(x.CalculationType))
			.WithMessage("Amount is required when calculationType is AMOUNT.");

		RuleFor(x => x.Percentage)
			.NotNull()
			.InclusiveBetween(0, 100)
			.When(x => IsPercentageCalculation(x.CalculationType))
			.WithMessage("Percentage must be between 0 and 100 when calculationType is PERCENTAGE.");

		RuleFor(x => x.SelectedDeductionComponentId)
			.NotNull()
			.When(x => IsPercentageCalculation(x.CalculationType))
			.WithMessage("selectedDeductionComponentId is required when calculationType is PERCENTAGE.");
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
}
