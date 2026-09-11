using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public abstract class AdmissionOfferingWriteDtoValidator<T> : AbstractValidator<T>
	where T : AdmissionOfferingWriteDto
{
	protected AdmissionOfferingWriteDtoValidator()
	{
		RuleFor(x => x.BoardId).NotEmpty();
		RuleFor(x => x.MediumId).NotEmpty();
		RuleFor(x => x.StageId).NotEmpty();
		RuleFor(x => x.StandardId).NotEmpty();
		RuleFor(x => x.Code).NotEmpty().MaximumLength(AdmissionOfferingConsts.MaxCodeLength);
		RuleFor(x => x.OpenDate)
			.NotNull()
			.When(x => !x.UseCycleDates)
			.WithMessage("Applications open date is required when Cycle dates are not used.");
		RuleFor(x => x.CloseDate)
			.NotNull()
			.When(x => !x.UseCycleDates)
			.WithMessage("Applications close date is required when Cycle dates are not used.");
		RuleFor(x => x)
			.Must(HaveValidDateRange)
			.WithMessage("Applications close date must be on or after the open date.");
	}

	private static bool HaveValidDateRange(T input) => input.UseCycleDates ||
		(input.OpenDate.HasValue && input.CloseDate.HasValue && input.OpenDate.Value.Date <= input.CloseDate.Value.Date);
}

public sealed class CreateAdmissionOfferingDtoValidator : AdmissionOfferingWriteDtoValidator<CreateAdmissionOfferingDto>;

public sealed class UpdateAdmissionOfferingDtoValidator : AdmissionOfferingWriteDtoValidator<UpdateAdmissionOfferingDto>
{
	public UpdateAdmissionOfferingDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
	}
}

public sealed class ValidateAdmissionOfferingDtoValidator : AdmissionOfferingWriteDtoValidator<ValidateAdmissionOfferingDto>
{
	public ValidateAdmissionOfferingDtoValidator()
	{
		RuleFor(x => x.AdmissionCycleId).NotEmpty();
	}
}

public sealed class CloneAdmissionOfferingDtoValidator : AbstractValidator<CloneAdmissionOfferingDto>
{
	public CloneAdmissionOfferingDtoValidator()
	{
		RuleFor(x => x.AdmissionCycleId).NotEmpty();
		RuleFor(x => x.BoardId).NotEmpty();
		RuleFor(x => x.MediumId).NotEmpty();
		RuleFor(x => x.StageId).NotEmpty();
		RuleFor(x => x.StandardId).NotEmpty();
		RuleFor(x => x.Code).NotEmpty().MaximumLength(AdmissionOfferingConsts.MaxCodeLength);
	}
}

public sealed class BulkCreateAdmissionOfferingsDtoValidator : AbstractValidator<BulkCreateAdmissionOfferingsDto>
{
	public BulkCreateAdmissionOfferingsDtoValidator()
	{
		RuleFor(x => x.BoardId).NotEmpty();
		RuleFor(x => x.MediumId).NotEmpty();
		RuleFor(x => x.StageId).NotEmpty();
		RuleFor(x => x.Items).NotEmpty().Must(items => items.Count <= 100)
			.WithMessage("Between 1 and 100 Admission Offerings can be created in one request.");
		RuleForEach(x => x.Items).ChildRules(item =>
		{
			item.RuleFor(x => x.StandardId).NotEmpty();
			item.RuleFor(x => x.Code).NotEmpty().MaximumLength(AdmissionOfferingConsts.MaxCodeLength);
		});
		RuleFor(x => x.Items).Must(items => items.ConvertAll(x => x.StandardId).Distinct().Count() == items.Count)
			.WithMessage("A Standard can appear only once in a bulk request.");
		RuleFor(x => x.OpenDate).NotNull().When(x => !x.UseCycleDates);
		RuleFor(x => x.CloseDate).NotNull().When(x => !x.UseCycleDates);
		RuleFor(x => x).Must(x => x.UseCycleDates ||
			(x.OpenDate.HasValue && x.CloseDate.HasValue && x.OpenDate.Value.Date <= x.CloseDate.Value.Date))
			.WithMessage("Applications close date must be on or after the open date.");
	}
}
