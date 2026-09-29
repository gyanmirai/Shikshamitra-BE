using System;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class ProvisionStudentPortalAccountDto
{
	public string AccountType { get; set; } = string.Empty;
	public string Relationship { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public bool RelationshipVerified { get; set; }
	public bool LinkExistingGuardian { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class ProvisionStudentPortalAccountDtoValidator : AbstractValidator<ProvisionStudentPortalAccountDto>
{
	public ProvisionStudentPortalAccountDtoValidator()
	{
		RuleFor(x => x.AccountType).Must(value => value is "student" or "guardian");
		RuleFor(x => x.Relationship).Must((input, value) => input.AccountType == "student"
			? value == "self" : value is "father" or "mother" or "guardian");
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
		RuleFor(x => x.RelationshipVerified).Equal(true).WithMessage("Verify the account owner and their relationship before granting portal access.");
	}
}

public class StudentPortalAccountDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public Guid UserId { get; set; }
	public string AccountType { get; set; } = string.Empty;
	public string Relationship { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public bool EmailConfirmed { get; set; }
	public bool IsActive { get; set; }
}
