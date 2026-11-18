using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(INotificationChannelSender))]
public sealed class SmtpNotificationChannelSender(IEmailSender emails) : INotificationChannelSender, ITransientDependency
{
	public CommunicationChannelType Channel => CommunicationChannelType.Email;

	public async Task<NotificationChannelSendResult> SendAsync(NotificationChannelSendRequest input, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (input.Channel != Channel || input.ProviderSettingId == Guid.Empty)
			return new(NotificationProviderAcceptance.Rejected, null, NotificationErrorCodes.ProviderUnavailable);

		var body = input.Body;
		if (!string.IsNullOrWhiteSpace(input.LinkUrl))
		{
			if (!Uri.TryCreate(input.LinkUrl, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps || link.UserInfo.Length != 0)
				return new(NotificationProviderAcceptance.Rejected, null, NotificationErrorCodes.TemplateInvalid);
			var href = $"href=\"{WebUtility.HtmlEncode(link.AbsoluteUri)}\"";
			if (!body.Contains(href, StringComparison.Ordinal))
				body += $"<p><a {href}>Open in ShikshaMitra</a></p>";
		}
		try
		{
			await emails.SendAsync(input.Destination, input.Subject ?? string.Empty, body, isBodyHtml: true);
			return new(NotificationProviderAcceptance.Accepted, $"smtp:{input.DispatchId:N}:{input.AttemptNumber}", null);
		}
		catch (SmtpException error) when ((int)error.StatusCode is >= 400 and < 600)
		{
			var temporary = (int)error.StatusCode < 500;
			return new(NotificationProviderAcceptance.Rejected, null,
				temporary ? "SmtpTemporaryRejection" : "SmtpPermanentRejection", IsTransient: temporary);
		}
		catch (Exception)
		{
			return new(NotificationProviderAcceptance.UnknownAcceptance, null, "SmtpAcceptanceUnknown");
		}
	}
}
