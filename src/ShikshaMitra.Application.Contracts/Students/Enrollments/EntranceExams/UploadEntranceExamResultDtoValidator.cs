using FluentValidation;

namespace ShikshaMitra.Students.Enrollments.EntranceExams;

public class UploadEntranceExamResultDtoValidator : AbstractValidator<UploadEntranceExamResultDto>
{
	public UploadEntranceExamResultDtoValidator()
	{
		RuleFor(x => x.TotalMarks)
			.InclusiveBetween(1.0, 2000.0);

		RuleFor(x => x.PassingMarks)
			.InclusiveBetween(0.0, 2000.0)
			.LessThanOrEqualTo(x => x.TotalMarks)
			.WithMessage("Passing marks must not exceed total marks.");

		RuleFor(x => x.ObtainedMarks)
			.InclusiveBetween(0.0, 2000.0)
			.LessThanOrEqualTo(x => x.TotalMarks)
			.WithMessage("Obtained marks must not exceed total marks.");
	}
}
