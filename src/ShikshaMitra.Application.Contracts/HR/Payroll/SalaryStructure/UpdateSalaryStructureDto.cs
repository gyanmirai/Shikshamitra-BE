using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.HR.Employees.Payroll;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// DTO for updating employee salary structure.
/// </summary>
public class UpdateSalaryStructureDto : IValidatableObject
{
	/// <summary>
	/// List of earning components.
	/// </summary>
	[Required]
	public List<SalaryComponentDto> EarningComponents { get; set; } = new();
	
	/// <summary>
	/// List of deduction components.
	/// </summary>
	[Required]
	public List<SalaryComponentDto> DeductionComponents { get; set; } = new();

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		// Validate earning components
		for (int i = 0; i < EarningComponents.Count; i++)
		{
			foreach (var error in ValidateComponent(EarningComponents[i], nameof(EarningComponents), i))
			{
				yield return error;
			}
		}

		// Validate deduction components
		for (int i = 0; i < DeductionComponents.Count; i++)
		{
			foreach (var error in ValidateComponent(DeductionComponents[i], nameof(DeductionComponents), i))
			{
				yield return error;
			}
		}
	}

	private static IEnumerable<ValidationResult> ValidateComponent(SalaryComponentDto component, string arrayName, int index)
	{
		if (string.IsNullOrWhiteSpace(component.Name))
		{
			yield return new ValidationResult(
				$"{arrayName} component at index {index} must have a name.",
				new[] { $"{arrayName}[{index}].{nameof(SalaryComponentDto.Name)}" }
			);
		}

		if (component.Amount < 0)
		{
			yield return new ValidationResult(
				$"{arrayName} component '{component.Name}' cannot have negative amount.",
				new[] { $"{arrayName}[{index}].{nameof(SalaryComponentDto.Amount)}" }
			);
		}

		// Validate DependOn is required when ComponentType is Percentage
		if (component.ComponentType == SalaryComponentType.Percentage && component.DependOn == null)
		{
			yield return new ValidationResult(
				$"{arrayName} component '{component.Name}' requires DependOn when ComponentType is Percentage.",
				new[] { $"{arrayName}[{index}].{nameof(SalaryComponentDto.DependOn)}" }
			);
		}
	}
}

