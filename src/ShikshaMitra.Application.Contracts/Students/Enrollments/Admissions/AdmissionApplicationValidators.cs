using System;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class StartAdmissionDirectRegistrationDtoValidator : AbstractValidator<StartAdmissionDirectRegistrationDto>
{
	public StartAdmissionDirectRegistrationDtoValidator()
	{
		RuleFor(input => input.RequestId).NotEmpty();
		RuleFor(input => input.AdmissionOfferingId).NotEmpty();
		RuleFor(input => input.CounsellorId).NotEqual(Guid.Empty)
			.When(input => input.CounsellorId.HasValue);
		RuleFor(input => input.ApplicantFirstName).NotEmpty().MaximumLength(128);
		RuleFor(input => input.ApplicantMiddleName).MaximumLength(128);
		RuleFor(input => input.ApplicantLastName).MaximumLength(128);
		RuleFor(input => input.ApplicantDateOfBirth).NotNull()
			.Must(value => value.HasValue && value.Value.Date <= DateTime.UtcNow.Date)
			.WithMessage("Enter a valid date of birth that is not in the future.");
		RuleFor(input => input.ApplicantGender).NotEmpty()
			.Must(value => Enum.TryParse<GenderEnum>(value, true, out var gender) && Enum.IsDefined(gender))
			.WithMessage("Choose a supported gender.");
		RuleFor(input => input.GuardianFirstName).NotEmpty().MaximumLength(128);
		RuleFor(input => input.GuardianLastName).MaximumLength(128);
		RuleFor(input => input.GuardianMobile).NotEmpty().MaximumLength(32);
		RuleFor(input => input.GuardianEmail).EmailAddress().MaximumLength(256)
			.When(input => !string.IsNullOrWhiteSpace(input.GuardianEmail));
		RuleFor(input => input.GuardianRelationship).IsInEnum();
		RuleFor(input => input.ExistingFamilyId).NotEqual(Guid.Empty)
			.When(input => input.ExistingFamilyId.HasValue);
		RuleFor(input => input.CreateSeparateFamily).Equal(false)
			.When(input => input.ExistingFamilyId.HasValue)
			.WithMessage("Choose an existing family or create a separate family, not both.");
		RuleFor(input => input.SeparateFamilyReason).MaximumLength(512);
	}
}

public class AdvanceAdmissionWorkflowDtoValidator : AbstractValidator<AdvanceAdmissionWorkflowDto>
{
	public AdvanceAdmissionWorkflowDtoValidator()
	{
		RuleFor(input => input.ExpectedCurrentStageInstanceId).NotEmpty();
		RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
		RuleFor(input => input.ConcurrencyStamp).NotEmpty();
		RuleFor(input => input.Reason).MaximumLength(512);
		RuleFor(input => input.Reason)
			.NotEmpty()
			.When(input => input.SkipCurrentStage || input.AdminOverride || input.DeferFeeCollection)
			.WithMessage("A reason is required when deferring collection, skipping or overriding workflow routing.");
		RuleFor(input => input.DeferFeeCollection).Equal(false)
			.When(input => input.SkipCurrentStage || input.AdminOverride)
			.WithMessage("Collection deferral cannot be combined with skipping or overriding workflow routing.");
	}
}

public class UpdateAdmissionApplicationFormValuesDtoValidator : AbstractValidator<UpdateAdmissionApplicationFormValuesDto>
{
	public UpdateAdmissionApplicationFormValuesDtoValidator()
	{
		RuleFor(input => input.ConcurrencyStamp).NotEmpty();
		RuleForEach(input => input.Values).ChildRules(value =>
		{
			value.RuleFor(item => item.FieldCode).NotEmpty().MaximumLength(128);
		});
	}
}

public class AdmissionApplicationDecisionDtoValidator : AbstractValidator<AdmissionApplicationDecisionDto>
{
	public AdmissionApplicationDecisionDtoValidator()
	{
		RuleFor(input => input.ConcurrencyStamp).NotEmpty();
		RuleFor(input => input.Reason).MaximumLength(512);
		RuleFor(input => input.CounsellorId).NotEqual(Guid.Empty)
			.When(input => input.CounsellorId.HasValue);
	}
}

public class EndAdmissionJourneyDtoValidator : AbstractValidator<EndAdmissionJourneyDto>
{
	public EndAdmissionJourneyDtoValidator()
	{
		RuleFor(input => input.ConcurrencyStamp).NotEmpty();
		RuleFor(input => input.Reason).NotEmpty().MaximumLength(512);
	}
}
