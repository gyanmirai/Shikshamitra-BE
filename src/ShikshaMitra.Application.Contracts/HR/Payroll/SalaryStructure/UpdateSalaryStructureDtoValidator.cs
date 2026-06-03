using FluentValidation;
using FluentValidation.Results;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class UpdateSalaryStructureDtoValidator : AbstractValidator<UpdateSalaryStructureDto>
{
	public UpdateSalaryStructureDtoValidator()
	{
		RuleFor(x => x.EarningComponents).NotEmpty();
		RuleFor(x => x.DeductionComponents).NotEmpty();

		RuleForEach(x => x.EarningComponents).SetValidator(new SalaryComponentDtoValidator());
		RuleForEach(x => x.DeductionComponents).SetValidator(new SalaryComponentDtoValidator());

		RuleFor(x => x).Custom((input, context) =>
		{
			AddComponentFailures(context, input.EarningComponents, nameof(UpdateSalaryStructureDto.EarningComponents));
			AddComponentFailures(context, input.DeductionComponents, nameof(UpdateSalaryStructureDto.DeductionComponents));
		});
	}

	private static void AddComponentFailures(
		ValidationContext<UpdateSalaryStructureDto> context,
		IReadOnlyList<SalaryComponentDto> components,
		string arrayName)
	{
		for (var i = 0; i < components.Count; i++)
		{
			var component = components[i];
			if (string.IsNullOrWhiteSpace(component.Name))
			{
				context.AddFailure(new ValidationFailure(
					$"{arrayName}[{i}].{nameof(SalaryComponentDto.Name)}",
					$"{arrayName} component at index {i} must have a name."));
			}

			if (component.Amount.HasValue && component.Amount.Value < 0)
			{
				context.AddFailure(new ValidationFailure(
					$"{arrayName}[{i}].{nameof(SalaryComponentDto.Amount)}",
					$"{arrayName} component '{component.Name}' cannot have negative amount."));
			}
		}
	}
}
