using System;
using System.Threading.Tasks;
using FluentValidation;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Notifications;

public interface ILibraryNotificationRecipientLookupAppService : IApplicationService
{
	Task<PaginatedResponse<LibraryNotificationRecipientOptionDto>> SearchAsync(LibraryNotificationRecipientSearchRequest input);
}

public class LibraryNotificationRecipientSearchRequest
{
	public string? Search { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
}

public class LibraryNotificationRecipientSearchRequestValidator : AbstractValidator<LibraryNotificationRecipientSearchRequest>
{
	public LibraryNotificationRecipientSearchRequestValidator()
	{
		RuleFor(input => input.Search).MaximumLength(100);
		RuleFor(input => input.Page).GreaterThan(0);
		RuleFor(input => input.PageSize).InclusiveBetween(1, 100);
	}
}

public class LibraryNotificationRecipientOptionDto
{
	public Guid Id { get; set; }
	public string? MemberIdentifier { get; set; }
	public string MemberName { get; set; } = string.Empty;
	public int Role { get; set; }
	public bool IsActiveMembership { get; set; }
	public bool HasRecoverableObligation { get; set; }
	public bool IsEligibleForNotification { get; set; }
	public string? EligibilityReason { get; set; }
}
