using FluentValidation;

namespace ShikshaMitra.Students.Enrollments.Admissions;

public class BulkMoveToAdmissionDtoValidator : AbstractValidator<BulkMoveToAdmissionDto>
{
	public BulkMoveToAdmissionDtoValidator()
	{
		RuleFor(x => x.StudentIds)
			.NotEmpty()
			.WithMessage("At least one student ID is required.");
	}
}
