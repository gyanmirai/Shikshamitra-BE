using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public sealed class NotificationSourceLink(IConfiguration configuration, NotificationTemplateVariableValidator validator) : ITransientDependency
{
	internal string? Resolve(NotificationSourceSnapshot snapshot, NotificationRecipientRefDto sourceRecipient)
	{
		var specific = snapshot.RecipientContent?.SingleOrDefault(x => x.Recipient == sourceRecipient);
		return Resolve(specific == null ? snapshot.RelativeLink : specific.RelativeLink);
	}

	internal string? Resolve(string? relativeLink)
	{
		if (relativeLink == null || !relativeLink.StartsWith('/') || relativeLink.StartsWith("//", StringComparison.Ordinal) ||
			relativeLink.Contains('\\') || relativeLink.Any(char.IsControl) ||
			!Uri.TryCreate(configuration["App:ClientUrl"], UriKind.Absolute, out var origin) ||
			!Uri.TryCreate(origin, relativeLink, out var link) || !validator.IsAllowedLink(link.AbsoluteUri)) return null;
		return link.AbsoluteUri;
	}
}
