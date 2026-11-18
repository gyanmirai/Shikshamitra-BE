using System;
using System.Globalization;
using FluentValidation;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationPreferenceValidator : AbstractValidator<SaveNotificationPreferenceDto>
{
	public NotificationPreferenceValidator()
	{
		RuleFor(x => x.Category).NotEmpty().MaximumLength(80);
		RuleFor(x => x.StudentId).NotEqual(Guid.Empty).When(x => x.StudentId.HasValue);
		RuleFor(x => x.EnabledChannels).NotNull().Must(x => x != null && x.Count <= 4);
		RuleForEach(x => x.EnabledChannels).IsInEnum().NotEqual(CommunicationChannelType.Sms);
		RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(16).Must(ValidLanguage);
		RuleFor(x => x.ConcurrencyStamp).MaximumLength(40);
		When(x => x.QuietHoursEnabled, () => {
			RuleFor(x => x.QuietHoursStart).Must(ValidTime);
			RuleFor(x => x.QuietHoursEnd).Must(ValidTime).NotEqual(x => x.QuietHoursStart);
		});
	}
	private static bool ValidTime(string? value) => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
	private static bool ValidLanguage(string value)
	{
		try { return !string.IsNullOrWhiteSpace(value) && CultureInfo.GetCultureInfo(value).Name.Length != 0; }
		catch (CultureNotFoundException) { return false; }
	}
}
