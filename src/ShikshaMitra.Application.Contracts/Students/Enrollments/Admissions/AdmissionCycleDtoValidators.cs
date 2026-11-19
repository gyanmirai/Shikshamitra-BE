using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class CreateUpdateAdmissionCycleDtoValidator : AbstractValidator<CreateUpdateAdmissionCycleDto>
{
	public CreateUpdateAdmissionCycleDtoValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(AdmissionCycleConsts.MaxNameLength);
		RuleFor(x => x.Code).MaximumLength(AdmissionCycleConsts.MaxCodeLength);
		RuleFor(x => x.Description).MaximumLength(AdmissionCycleConsts.MaxDescriptionLength);
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.EndDate)
			.GreaterThan(x => x.StartDate)
			.WithMessage("Applications close date must be after the open date.");
	}
}

public class AdmissionCycleActionDtoValidator : AbstractValidator<AdmissionCycleActionDto>
{
	public AdmissionCycleActionDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(AdmissionCycleConsts.MaxReasonLength);
	}
}

public class ReopenAdmissionCycleDtoValidator : AbstractValidator<ReopenAdmissionCycleDto>
{
	public ReopenAdmissionCycleDtoValidator()
	{
		Include(new AdmissionCycleActionDtoValidator());
	}
}

public class UpdateAdmissionCycleWindowDtoValidator : AbstractValidator<UpdateAdmissionCycleWindowDto>
{
	public UpdateAdmissionCycleWindowDtoValidator()
	{
		Include(new AdmissionCycleActionDtoValidator());
		RuleFor(x => x.EndDate)
			.GreaterThan(x => x.StartDate)
			.WithMessage("Applications close date must be after the open date.");
	}
}

public class ScheduleAdmissionCycleDtoValidator : AbstractValidator<ScheduleAdmissionCycleDto>
{
	public ScheduleAdmissionCycleDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x).Must(x => x.PublishAt.HasValue || x.CloseAt.HasValue)
			.WithMessage("Select a publication or closure schedule.");
	}
}

public class CloneAdmissionCycleDtoValidator : AbstractValidator<CloneAdmissionCycleDto>
{
	public CloneAdmissionCycleDtoValidator()
	{
		Include(new CreateUpdateAdmissionCycleDtoValidator());
		RuleFor(x => x.SourceCycleId).NotEmpty();
	}
}

public class UpdateAdmissionCycleOfferingCapacityDtoValidator : AbstractValidator<UpdateAdmissionCycleOfferingCapacityDto>
{
	public UpdateAdmissionCycleOfferingCapacityDtoValidator()
	{
		RuleFor(x => x.Capacity).InclusiveBetween(1, 100000);
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(AdmissionCycleConsts.MaxReasonLength);
	}
}
