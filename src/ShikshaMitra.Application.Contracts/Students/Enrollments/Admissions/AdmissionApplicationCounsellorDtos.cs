using System;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AssignAdmissionApplicationCounsellorDto
{
	public Guid CounsellorId { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public string? Reason { get; set; }
}

public class AssignAdmissionApplicationCounsellorDtoValidator : AbstractValidator<AssignAdmissionApplicationCounsellorDto>
{
	public AssignAdmissionApplicationCounsellorDtoValidator()
	{
		RuleFor(input => input.CounsellorId).NotEmpty();
		RuleFor(input => input.ConcurrencyStamp).NotEmpty();
		RuleFor(input => input.Reason).MaximumLength(512);
	}
}
