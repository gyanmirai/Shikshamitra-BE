using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed class CreateUpdateAdmissionCampusDtoValidator : AbstractValidator<CreateUpdateAdmissionCampusDto>
{
	public CreateUpdateAdmissionCampusDtoValidator()
	{
		RuleFor(x => x.Code).NotEmpty().MaximumLength(AdmissionCampusConsts.MaxCodeLength);
		RuleFor(x => x.Name).NotEmpty().MaximumLength(AdmissionCampusConsts.MaxNameLength);
	}
}
