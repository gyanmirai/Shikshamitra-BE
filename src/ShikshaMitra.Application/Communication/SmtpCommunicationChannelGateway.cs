using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace ShikshaMitra.Communication;

/// <summary>Uses the school's existing ABP SMTP settings when the enabled provider is named SMTP.</summary>
public class SmtpCommunicationChannelGateway(IEmailSender emails) : ICommunicationChannelGateway, ITransientDependency
{
	public bool CanHandle(CommunicationChannelType channel, string providerName) =>
		channel == CommunicationChannelType.Email && string.Equals(providerName?.Trim(), "SMTP", StringComparison.OrdinalIgnoreCase);

	public async Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request)
	{
		try
		{
			var isBodyHtml = !string.IsNullOrWhiteSpace(request.HtmlBody);
			var body = isBodyHtml ? request.HtmlBody! :
				string.IsNullOrWhiteSpace(request.LinkUrl) ? request.Body : request.Body + "\n\n" + request.LinkUrl;
			await emails.SendAsync(request.Destination, request.Subject ?? string.Empty, body, isBodyHtml);
			return new CommunicationSendResult
			{
				Status = CommunicationDeliveryStatus.Sent, SentAt = DateTimeOffset.UtcNow,
				ProviderMessageId = $"smtp:{request.CampaignId:N}:{request.RecipientSnapshotId:N}:{request.AttemptNumber}",
				EstimatedCost = 0
			};
		}
		catch (SmtpException error) when ((int)error.StatusCode >= 400)
		{
			return new CommunicationSendResult
			{
				Status = CommunicationDeliveryStatus.Failed,
				ErrorCode = "SMTP_SEND_FAILED",
				ErrorMessage = "The SMTP provider could not accept this email. Review the school's email settings."
			};
		}
		catch (Exception)
		{
			// A disconnect/timeout can occur after acceptance. Do not classify it as a
			// safe retry or expose transport text, which can contain credentials or links.
			return new CommunicationSendResult
			{
				Status = CommunicationDeliveryStatus.Failed,
				ErrorCode = "SMTP_SEND_UNCONFIRMED",
				ErrorMessage = "The email sender's acceptance could not be confirmed. Review delivery history before sending again."
			};
		}
	}
}
