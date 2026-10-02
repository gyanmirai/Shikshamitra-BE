using FluentValidation;
using FluentValidation.Results;
using System.Collections.Generic;
using System;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class UpdateSalaryStructureDtoValidator : AbstractValidator<UpdateSalaryStructureDto>
{
	public UpdateSalaryStructureDtoValidator()
	{
		// Recurring salary lives in compensation revisions; one-time additions may be empty.
		RuleFor(x => x.EarningComponents).NotNull();
		RuleFor(x => x.DeductionComponents).NotNull();

		RuleForEach(x => x.EarningComponents).SetValidator(new SalaryComponentDtoValidator());
		RuleForEach(x => x.DeductionComponents).SetValidator(new SalaryComponentDtoValidator());
		RuleFor(x => x.QuotedSalaryBasis)
			.Must(x => x is "GROSS" or "EMPLOYER_COST")
			.When(x => !string.IsNullOrWhiteSpace(x.QuotedSalaryBasis))
			.WithMessage("Quoted salary basis must be GROSS or EMPLOYER_COST.");
		RuleFor(x => x.QuotedMonthlyAmount)
			.GreaterThan(0m)
			.LessThanOrEqualTo(100_000_000m)
			.When(x => x.QuotedMonthlyAmount.HasValue);
		RuleFor(x => x)
			.Must(HasConsistentGenerationProvenance)
			.WithMessage("Salary generation provenance must include the quote, template, version, and generation timestamp together.");

		RuleFor(x => x).Custom((input, context) =>
		{
			AddComponentFailures(context, input.EarningComponents, nameof(UpdateSalaryStructureDto.EarningComponents));
			AddComponentFailures(context, input.DeductionComponents, nameof(UpdateSalaryStructureDto.DeductionComponents));
		});
	}

	private static bool HasConsistentGenerationProvenance(UpdateSalaryStructureDto input)
	{
		var hasAny = input.SourceSalaryTemplateId.HasValue ||
			input.SourceSalaryTemplateVersionId.HasValue ||
			input.SourceSalaryTemplateVersion.HasValue ||
			input.SalaryGeneratedAt.HasValue;
		return !hasAny ||
			input.SourceSalaryTemplateId is { } templateId && templateId != Guid.Empty &&
			input.SourceSalaryTemplateVersionId is { } versionId && versionId != Guid.Empty &&
			input.SourceSalaryTemplateVersion > 0 &&
			input.SalaryGeneratedAt.HasValue &&
			input.QuotedMonthlyAmount > 0m &&
			input.QuotedSalaryBasis is "GROSS" or "EMPLOYER_COST";
	}

	private static void AddComponentFailures(
		ValidationContext<UpdateSalaryStructureDto> context,
		IReadOnlyList<SalaryComponentDto>? components,
		string arrayName)
	{
		if (components == null)
		{
			return;
		}

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
