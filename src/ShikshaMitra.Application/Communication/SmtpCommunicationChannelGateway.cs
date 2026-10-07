using System;
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
			var body = string.IsNullOrWhiteSpace(request.LinkUrl) ? request.Body : request.Body + "\n\n" + request.LinkUrl;
			await emails.SendAsync(request.Destination, request.Subject ?? string.Empty, body, isBodyHtml: false);
			return new CommunicationSendResult
			{
				Status = CommunicationDeliveryStatus.Sent, SentAt = DateTimeOffset.UtcNow,
				ProviderMessageId = $"smtp:{request.CampaignId:N}:{request.RecipientSnapshotId:N}:{request.AttemptNumber}",
				EstimatedCost = 0
			};
		}
		catch (Exception)
		{
			return new CommunicationSendResult
			{
				Status = CommunicationDeliveryStatus.Failed,
				ErrorCode = "SMTP_SEND_FAILED",
				ErrorMessage = "The SMTP provider could not accept this email. Review the school's email settings."
			};
		}
	}
}
