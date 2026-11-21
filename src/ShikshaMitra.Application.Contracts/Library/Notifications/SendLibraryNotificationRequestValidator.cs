using System;
using FluentValidation;

namespace ShikshaMitra.Library.Notifications;

public class SendLibraryNotificationRequestValidator : AbstractValidator<SendLibraryNotificationRequest>
{
	public SendLibraryNotificationRequestValidator()
	{
		RuleFor(request => request.Subject).MaximumLength(300);
		RuleFor(request => request.Message).MaximumLength(2000);
		RuleFor(request => request.RelatedEntityType).MaximumLength(80);
		RuleFor(request => request.Channel).Must(channel =>
			string.Equals(channel, LibraryNotificationChannels.InApp, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(channel, LibraryNotificationChannels.Email, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(channel, LibraryNotificationChannels.Sms, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(channel, LibraryNotificationChannels.WhatsApp, StringComparison.OrdinalIgnoreCase))
			.WithMessage("Choose a supported notification channel.");
		RuleFor(request => request.Recipients).NotNull();
	}
}
