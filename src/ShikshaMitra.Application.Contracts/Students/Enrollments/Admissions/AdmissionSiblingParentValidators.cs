using System;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class CreateAdmissionInquiryDtoValidator : AbstractValidator<CreateAdmissionInquiryDto>
{
	public CreateAdmissionInquiryDtoValidator()
	{
		RuleFor(input => input.ParentRelationship).Must(value => value is null or "father" or "mother").WithMessage("Choose Father or Mother.");
		RuleFor(input => input.ExistingParentApplicantId).NotEqual(Guid.Empty).When(input => input.ExistingParentApplicantId.HasValue);
		RuleFor(input => input.ExistingParentApplicantId).NotNull().When(input => input.ExistingFamilyId.HasValue && input.ParentRelationship != null);
		RuleFor(input => input.ExistingFamilyId).NotNull().NotEqual(Guid.Empty).When(input => input.ExistingParentApplicantId.HasValue);
		RuleFor(input => input.ParentRelationship).NotEmpty().When(input => input.ExistingParentApplicantId.HasValue);
		RuleFor(input => input.CreateSeparateFamily).Equal(false).When(input => input.ExistingFamilyId.HasValue);
	}
}

public class AdmissionInquiryDuplicateCheckInputValidator : AbstractValidator<AdmissionInquiryDuplicateCheckInput>
{
	public AdmissionInquiryDuplicateCheckInputValidator()
	{
		RuleFor(input => input.ParentContacts).NotNull().Must(contacts => contacts is { Count: <= 2 });
		RuleForEach(input => input.ParentContacts).ChildRules(contact =>
		{
			contact.RuleFor(parent => parent.Relationship).Must(value => value is "father" or "mother").WithMessage("Choose Father or Mother.");
			contact.RuleFor(parent => parent.Email).MaximumLength(256).EmailAddress().When(parent => !string.IsNullOrWhiteSpace(parent.Email));
			contact.RuleFor(parent => parent.Mobile).MaximumLength(32);
		});
	}
}
