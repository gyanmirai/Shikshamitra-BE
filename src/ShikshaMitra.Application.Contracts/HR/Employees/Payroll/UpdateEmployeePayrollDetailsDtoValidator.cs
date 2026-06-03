using FluentValidation;
using FluentValidation.Results;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Employees.Payroll;

public class UpdateEmployeePayrollDetailsDtoValidator : AbstractValidator<UpdateEmployeePayrollDetailsDto>
{
	public UpdateEmployeePayrollDetailsDtoValidator()
	{
		RuleFor(x => x.Earnings).NotEmpty();
		RuleFor(x => x.Deductions).NotEmpty();

		RuleFor(x => x).Custom((input, context) =>
		{
			if (!string.IsNullOrWhiteSpace(input.PayrollDetails.AccountNumber)
				&& !IsNumericString(input.PayrollDetails.AccountNumber))
			{
				context.AddFailure(new ValidationFailure(
					nameof(PayrollDetailsDto.AccountNumber),
					"Account number must be numeric."));
			}

			if (!string.IsNullOrWhiteSpace(input.PayrollDetails.IfscCode)
				&& !IsValidIfscCode(input.PayrollDetails.IfscCode))
			{
				context.AddFailure(new ValidationFailure(
					nameof(PayrollDetailsDto.IfscCode),
					"IFSC code must be in valid format (e.g., HDFC0001234)."));
			}

			AddComponentFailures(context, input.Earnings, nameof(UpdateEmployeePayrollDetailsDto.Earnings));
			AddComponentFailures(context, input.Deductions, nameof(UpdateEmployeePayrollDetailsDto.Deductions));
		});
	}

	private static void AddComponentFailures(
		ValidationContext<UpdateEmployeePayrollDetailsDto> context,
		IReadOnlyList<PayrollComponentDto> components,
		string arrayName)
	{
		for (var i = 0; i < components.Count; i++)
		{
			var component = components[i];
			if (string.IsNullOrWhiteSpace(component.Name))
			{
				context.AddFailure(new ValidationFailure(
					$"{arrayName}[{i}].{nameof(PayrollComponentDto.Name)}",
					$"{arrayName} component at index {i} must have a name."));
			}

			if (component.Amount < 0)
			{
				context.AddFailure(new ValidationFailure(
					$"{arrayName}[{i}].{nameof(PayrollComponentDto.Amount)}",
					$"{arrayName} component '{component.Name}' cannot have negative amount."));
			}

			if (component.ComponentType == SalaryComponentType.Percentage && component.DependOn == null)
			{
				context.AddFailure(new ValidationFailure(
					$"{arrayName}[{i}].{nameof(PayrollComponentDto.DependOn)}",
					$"{arrayName} component '{component.Name}' requires DependOn when ComponentType is Percentage."));
			}
		}
	}

	private static bool IsNumericString(string value)
	{
		return !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);
	}

	private static bool IsValidIfscCode(string ifscCode)
	{
		if (string.IsNullOrWhiteSpace(ifscCode) || ifscCode.Length != 11)
		{
			return false;
		}

		if (!ifscCode[..4].All(char.IsLetter))
		{
			return false;
		}

		if (ifscCode[4] != '0')
		{
			return false;
		}

		return ifscCode[5..].All(char.IsLetterOrDigit);
	}
}
