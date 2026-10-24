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
	public StudentSiblingParentContextDto? ParentSibling { get; set; }
}

public class StudentSiblingParentSelectionDto
{
	public string Relationship { get; set; } = string.Empty;
	public string ExpectedEmail { get; set; } = string.Empty;
	public Guid ExpectedUserId { get; set; }
}

public class RetryStudentParentSetupDto
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class LinkStudentSiblingParentDto
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public StudentSiblingParentSelectionDto? SiblingParentSelection { get; set; }
}

public class StudentSiblingParentSelectionDtoValidator : AbstractValidator<StudentSiblingParentSelectionDto>
{
	public StudentSiblingParentSelectionDtoValidator()
	{
		RuleFor(input => input.Relationship).Must(value => value is "father" or "mother");
		RuleFor(input => input.ExpectedEmail).NotEmpty().EmailAddress().MaximumLength(256);
		RuleFor(input => input.ExpectedUserId).NotEmpty();
	}
}

public class StudentSiblingParentCandidateDto
{
	public string Relationship { get; set; } = string.Empty;
	public string? Name { get; set; }
	public string Email { get; set; } = string.Empty;
	public Guid UserId { get; set; }
	public string SetupStatus { get; set; } = "Pending";
}

public class StudentSiblingParentContextDto
{
	public bool Enabled { get; set; }
	public bool FamilyConfirmed { get; set; }
	public bool IsLinked { get; set; }
	public bool RequiresSelection { get; set; }
	public string Status { get; set; } = "NewParent";
	public string? LinkedRelationship { get; set; }
	public string? SelectedRelationship { get; set; }
	public string? BlockedReason { get; set; }
	public bool CanRetrySetup { get; set; }
	public bool CanLink { get; set; }
	public IReadOnlyList<StudentSiblingParentCandidateDto> Candidates { get; set; } = [];
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
	public bool CanEditEmail { get; set; }
	public bool HasLinkedAccount { get; set; }
	public int LinkedStudentCount { get; set; }
	public string? PendingEmail { get; set; }
	public string? EmailChangeStatus { get; set; }
	public string? EmailEditBlockedReason { get; set; }
	public string EmailEditToken { get; set; } = string.Empty;
}

public class UpdateStudentCredentialsEmailDto
{
	public string AccountType { get; set; } = string.Empty;
	public string Relationship { get; set; } = string.Empty;
	public string? ExpectedEmail { get; set; }
	public string Email { get; set; } = string.Empty;
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public bool ConfirmSharedParent { get; set; }
	public bool CancelPendingChange { get; set; }
	public string EmailEditToken { get; set; } = string.Empty;
}

public class UpdateStudentCredentialsEmailDtoValidator : AbstractValidator<UpdateStudentCredentialsEmailDto>
{
	public UpdateStudentCredentialsEmailDtoValidator()
	{
		RuleFor(x => x.AccountType).Must(x => x is "student" or "guardian");
		RuleFor(x => x.Relationship).Must((input, value) => input.AccountType == "student"
			? value == "self" : value is "father" or "mother");
		RuleFor(x => x.ExpectedEmail).MaximumLength(256);
		RuleFor(x => x.Email).MaximumLength(256);
		RuleFor(x => x.Email).NotEmpty().EmailAddress()
			.Must(x => x != null && !x.Contains('\r') && !x.Contains('\n')).When(x => !x.CancelPendingChange);
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(100);
		RuleFor(x => x.EmailEditToken).NotEmpty().MaximumLength(100);
	}
}

public class ConfirmStudentPortalEmailDto
{
	public Guid TenantId { get; set; }
	public Guid UserId { get; set; }
	public Guid RequestId { get; set; }
	public string Token { get; set; } = string.Empty;
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
