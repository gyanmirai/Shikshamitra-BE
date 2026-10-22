using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentResultInputValidator : AbstractValidator<AdmissionAssessmentResultInput>
{
	public AdmissionAssessmentResultInputValidator()
	{
		RuleFor(input => input.SubjectScores).NotNull();
		RuleFor(input => input.SubjectScores).Must(scores => scores == null ||
			scores.All(score => score != null && !string.IsNullOrWhiteSpace(score.Code) && score.Code.Length <= 64 && (!score.Marks.HasValue || score.Marks >= 0)) &&
			scores.Select(score => score.Code).Distinct(StringComparer.Ordinal).Count() == scores.Count)
			.WithMessage("Enter each subject once, with a valid subject identifier and non-negative marks.");
		RuleFor(input => input.Notes).MaximumLength(1024);
	}
}
