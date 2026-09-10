using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class FinalizeAdmissionApplicationDtoValidator : AbstractValidator<ShikshaMitra.Students.StudentEnrollments.Admissions.FinalizeAdmissionApplicationDto>
{
	public FinalizeAdmissionApplicationDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
		RuleFor(x => x).Must(x => !x.ExistingStudentId.HasValue || !x.ConfirmCreateNew)
			.WithMessage("Choose an existing Student or confirm a new identity, not both.");
	}
}

public class UpdateStudentOnboardingStepDtoValidator : AbstractValidator<UpdateStudentOnboardingStepDto>
{
	public UpdateStudentOnboardingStepDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.WaiverReason).NotEmpty().MaximumLength(1000)
			.When(x => x.Status == StudentOnboardingStepStatus.Waived);
		RuleFor(x => x.Detail).MaximumLength(1000);
	}
}

public class AssignStudentOnboardingOwnerDtoValidator : AbstractValidator<AssignStudentOnboardingOwnerDto>
{
	public AssignStudentOnboardingOwnerDtoValidator() => RuleFor(x => x.ConcurrencyStamp).NotEmpty();
}

public class CreateStudentIdentifierSeriesDtoValidator : AbstractValidator<CreateStudentIdentifierSeriesDto>
{
	public CreateStudentIdentifierSeriesDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
		RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
		RuleFor(x => x.Prefix).MaximumLength(40);
		RuleFor(x => x.Suffix).MaximumLength(40);
		RuleFor(x => x.IdentifierType)
			.Must(type => type is StudentIdentifierType.GrNumber or StudentIdentifierType.AdmissionNumber)
			.WithMessage("Only GR/admission-number series are supported by Student Onboarding.");
		RuleFor(x => x.Padding).InclusiveBetween(1, 20);
		RuleFor(x => x.StartingSequence).GreaterThan(0);
		RuleFor(x => x.MaximumSequence).GreaterThanOrEqualTo(x => x.StartingSequence)
			.When(x => x.MaximumSequence.HasValue);
		RuleFor(x => x.EffectiveFrom).NotEmpty();
		RuleFor(x => x.EffectiveUntil).GreaterThan(x => x.EffectiveFrom).When(x => x.EffectiveUntil.HasValue);
	}
}

public class SetStudentOnboardingPlacementDtoValidator : AbstractValidator<SetStudentOnboardingPlacementDto>
{
	public SetStudentOnboardingPlacementDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.DivisionId).NotEmpty();
	}
}

public class AllocateStudentOnboardingIdentifierDtoValidator : AbstractValidator<AllocateStudentOnboardingIdentifierDto>
{
	public AllocateStudentOnboardingIdentifierDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.RollNumber).GreaterThan(0);
		RuleFor(x => x.ManualValue).MaximumLength(100);
		RuleFor(x => x.OverrideReason).NotEmpty().MaximumLength(1000)
			.When(x => !string.IsNullOrWhiteSpace(x.ManualValue));
	}
}

public class ActivateStudentOnboardingDtoValidator : AbstractValidator<ActivateStudentOnboardingDto>
{
	public ActivateStudentOnboardingDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
	}
}

public class CancelStudentOnboardingDtoValidator : AbstractValidator<CancelStudentOnboardingDto>
{
	public CancelStudentOnboardingDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
	}
}

public class UpsertStudentOnboardingPolicyDtoValidator : AbstractValidator<UpsertStudentOnboardingPolicyDto>
{
	public UpsertStudentOnboardingPolicyDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
		RuleFor(x => x).Must(x => x.OfferingId.HasValue == x.StandardId.HasValue)
			.WithMessage("An override must identify both an offering and its standard.");
		RuleFor(x => x.SeatReservationMinutes).InclusiveBetween(5, 10080);
		RuleFor(x => x.SlaHours).InclusiveBetween(1, 8760);
	}
}
