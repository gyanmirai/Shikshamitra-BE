using FluentValidation;

namespace ShikshaMitra.Students.Enrollments.EntranceExams;

public class UploadEntranceExamResultDtoValidator : AbstractValidator<UploadEntranceExamResultDto>
{
	public UploadEntranceExamResultDtoValidator()
	{
		RuleFor(x => x.TotalMarks)
			.InclusiveBetween(1m, 1000m);

		RuleFor(x => x.PassingMarks)
			.InclusiveBetween(0m, 1000m)
			.LessThanOrEqualTo(x => x.TotalMarks)
			.WithMessage("Passing marks must not exceed total marks.");

		RuleFor(x => x.ObtainedMarks)
			.InclusiveBetween(0m, 1000m)
			.LessThanOrEqualTo(x => x.TotalMarks)
			.WithMessage("Obtained marks must not exceed total marks.");
	}
}
