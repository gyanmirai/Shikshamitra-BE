using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Saved recipients and authoritative preparation/delivery state for one scoped case.</summary>
public class StudentCredentialsWorkspaceDto
{
	public Guid CaseId { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public IReadOnlyList<StudentCredentialsRecipientDto> Recipients { get; set; } = [];
}

public class StudentCredentialsRecipientDto
{
	public string AccountType { get; set; } = string.Empty;
	public string Relationship { get; set; } = string.Empty;
	public string? Name { get; set; }
	public string? Email { get; set; }
	public bool CanSend { get; set; }
	public string? BlockedReason { get; set; }
	public StudentCredentialEmailDto? Delivery { get; set; }
}

/// <summary>The address is a drift guard, never an arbitrary delivery destination.</summary>
public class SendStudentCredentialsDto
{
	public string AccountType { get; set; } = string.Empty;
	public string Relationship { get; set; } = string.Empty;
	public string ExpectedEmail { get; set; } = string.Empty;
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class SendStudentCredentialsDtoValidator : AbstractValidator<SendStudentCredentialsDto>
{
	public SendStudentCredentialsDtoValidator()
	{
		RuleFor(input => input.AccountType).Must(value => value is "student" or "guardian");
		RuleFor(input => input.Relationship).Must((input, value) => input.AccountType == "student"
			? value == "self" : value is "father" or "mother" or "guardian");
		RuleFor(input => input.ExpectedEmail).NotEmpty().EmailAddress().MaximumLength(256);
		RuleFor(input => input.ConcurrencyStamp).NotEmpty().MaximumLength(100);
	}
}
