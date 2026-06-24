using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication;

public class CommunicationSendRequest
{
	public Guid CampaignId { get; set; }
	public Guid? RecipientSnapshotId { get; set; }
	public CommunicationChannelType Channel { get; set; }
	public string ProviderName { get; set; } = string.Empty;
	public string Destination { get; set; } = string.Empty;
	public string? Subject { get; set; }
	public string Body { get; set; } = string.Empty;
	public string? LinkUrl { get; set; }
	public string LanguageCode { get; set; } = "en";
	public int AttemptNumber { get; set; } = 1;
}

public class CommunicationSendResult
{
	public CommunicationDeliveryStatus Status { get; set; } = CommunicationDeliveryStatus.Queued;
	public string? ProviderMessageId { get; set; }
	public string? ErrorCode { get; set; }
	public string? ErrorMessage { get; set; }
	public decimal? EstimatedCost { get; set; }
	public string CurrencyCode { get; set; } = "INR";
	public DateTimeOffset? QueuedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? SentAt { get; set; }
}

public interface ICommunicationChannelGateway
{
	bool CanHandle(CommunicationChannelType channel, string providerName);
	Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request);
}

public class QueueOnlyCommunicationChannelGateway : ICommunicationChannelGateway, ITransientDependency
{
	public bool CanHandle(CommunicationChannelType channel, string providerName)
	{
		return true;
	}

	public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request)
	{
		var recipientKey = request.RecipientSnapshotId?.ToString("N") ?? "none";
		return Task.FromResult(new CommunicationSendResult
		{
			Status = CommunicationDeliveryStatus.Queued,
			ProviderMessageId = $"queued:{request.CampaignId:N}:{recipientKey}:{request.Channel}:{request.AttemptNumber}",
			QueuedAt = DateTimeOffset.UtcNow,
			CurrencyCode = "INR",
			EstimatedCost = 0
		});
	}
}
