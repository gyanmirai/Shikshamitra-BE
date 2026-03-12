using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// DTO for updating employee payroll details.
/// </summary>
public class UpdateEmployeePayrollDetailsDto : IValidatableObject
{
	public PayrollDetailsDto PayrollDetails { get; set; } = new();

	[Required]
	public List<PayrollComponentDto> Earnings { get; set; } = new();

	[Required]
	public List<PayrollComponentDto> Deductions { get; set; } = new();

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		// Validate account number is numeric if provided
		if (!string.IsNullOrWhiteSpace(PayrollDetails.AccountNumber))
		{
			if (!IsNumericString(PayrollDetails.AccountNumber))
			{
				yield return new ValidationResult(
					"Account number must be numeric.",
					new[] { nameof(PayrollDetails.AccountNumber) }
				);
			}
		}

		// Validate IFSC code format if provided
		if (!string.IsNullOrWhiteSpace(PayrollDetails.IfscCode))
		{
			if (!IsValidIfscCode(PayrollDetails.IfscCode))
			{
				yield return new ValidationResult(
					"IFSC code must be in valid format (e.g., HDFC0001234).",
					new[] { nameof(PayrollDetails.IfscCode) }
				);
			}
		}

		// Validate TDS percentage
		if (PayrollDetails.TdsPercentage.HasValue)
		{
			if (PayrollDetails.TdsPercentage < 0 || PayrollDetails.TdsPercentage > 100)
			{
				yield return new ValidationResult(
					"TDS percentage must be between 0 and 100.",
					new[] { nameof(PayrollDetails.TdsPercentage) }
				);
			}
		}

		// Validate earnings
		for (int i = 0; i < Earnings.Count; i++)
		{
			foreach (var error in ValidateComponent(Earnings[i], nameof(Earnings), i))
			{
				yield return error;
			}
		}

		// Validate deductions
		for (int i = 0; i < Deductions.Count; i++)
		{
			foreach (var error in ValidateComponent(Deductions[i], nameof(Deductions), i))
			{
				yield return error;
			}
		}
	}

	private static IEnumerable<ValidationResult> ValidateComponent(PayrollComponentDto component, string arrayName, int index)
	{
		if (string.IsNullOrWhiteSpace(component.Name))
		{
			yield return new ValidationResult(
				$"{arrayName} component at index {index} must have a name.",
				new[] { $"{arrayName}[{index}].{nameof(PayrollComponentDto.Name)}" }
			);
		}

		if (component.Amount < 0)
		{
			yield return new ValidationResult(
				$"{arrayName} component '{component.Name}' cannot have negative amount.",
				new[] { $"{arrayName}[{index}].{nameof(PayrollComponentDto.Amount)}" }
			);
		}

		// Validate DependOn is required when ComponentType is Percentage
		if (component.ComponentType == SalaryComponentType.Percentage && component.DependOn == null)
		{
			yield return new ValidationResult(
				$"{arrayName} component '{component.Name}' requires DependOn when ComponentType is Percentage.",
				new[] { $"{arrayName}[{index}].{nameof(PayrollComponentDto.DependOn)}" }
			);
		}
	}

	private static bool IsNumericString(string value)
	{
		return !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);
	}

	private static bool IsValidIfscCode(string ifscCode)
	{
		// IFSC code format: 4 letters + 0 + 6 alphanumeric characters
		// Example: HDFC0001234
		if (string.IsNullOrWhiteSpace(ifscCode) || ifscCode.Length != 11)
		{
			return false;
		}

		// First 4 characters must be letters
		if (!ifscCode.Substring(0, 4).All(char.IsLetter))
		{
			return false;
		}

		// 5th character must be 0
		if (ifscCode[4] != '0')
		{
			return false;
		}

		// Last 6 characters must be alphanumeric
		if (!ifscCode.Substring(5).All(char.IsLetterOrDigit))
		{
			return false;
		}

		return true;
	}
}
