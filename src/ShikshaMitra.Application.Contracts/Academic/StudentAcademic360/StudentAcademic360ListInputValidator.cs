using FluentValidation;

namespace ShikshaMitra.Academic.StudentAcademic360;

public class StudentAcademic360ListInputValidator : AbstractValidator<GetStudentAcademic360ListInput>
{
	private const int MaximumPageSize = 100;

	public StudentAcademic360ListInputValidator()
	{
		RuleFor(input => input.AcademicYearId).NotEmpty();
		RuleFor(input => input.BoardId).NotEmpty();
		RuleFor(input => input.EducationMediumId).NotEmpty();
		RuleFor(input => input.MaxResultCount).InclusiveBetween(1, MaximumPageSize);
		RuleFor(input => input.SkipCount).GreaterThanOrEqualTo(0);
		RuleFor(input => input.SearchTerm).MaximumLength(200);
		RuleFor(input => input.DivisionId)
			.Must((input, _) => input.StandardId.HasValue)
			.When(input => input.DivisionId.HasValue)
			.WithMessage("A standard is required when a division is selected.");
	}
}
