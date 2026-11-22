using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;

namespace ShikshaMitra.Application.Communication;

public partial class CommunicationAppService
{
	private sealed record GenericPayloadQueries(IQueryable<CommunicationCampaign> Campaigns,
		IQueryable<DeliveryAttempt> Attempts, IQueryable<CommunicationReply> Replies,
		IQueryable<Guid> CampaignIds, IQueryable<Guid> RecipientIds);
	private sealed record GenericOperationalCounts(int Total, int Draft, int Pending, int Scheduled, int Queued,
		int Sent, int Failed, int Replies, int Attempts, int AttemptFailures);

	private static IQueryable<CommunicationCampaign> GenericCampaigns(IQueryable<CommunicationCampaign> query) =>
		query.Where(x => x.CampaignType == null || x.CampaignType.Trim().ToLower() != NotificationCampaignTypes.AcademicNormalized);

	private async Task<GenericPayloadQueries> GetGenericPayloadQueriesAsync()
	{
		var campaigns = GenericCampaigns(await _campaignRepository.GetQueryableAsync());
		var campaignIds = campaigns.Select(x => x.Id);
		var recipientIds = (await _recipientSnapshotRepository.GetQueryableAsync()).Where(x => campaignIds.Contains(x.CampaignId)).Select(x => x.Id);
		var attempts = (await _deliveryAttemptRepository.GetQueryableAsync()).Where(x => !x.IsDeleted && campaignIds.Contains(x.CampaignId) &&
			(!x.RecipientSnapshotId.HasValue || recipientIds.Contains(x.RecipientSnapshotId.Value)));
		var attemptIds = attempts.Select(x => x.Id);
		var replies = (await _replyRepository.GetQueryableAsync()).Where(x => !x.IsDeleted &&
			(!x.CampaignId.HasValue || campaignIds.Contains(x.CampaignId.Value)) &&
			(!x.DeliveryAttemptId.HasValue || attemptIds.Contains(x.DeliveryAttemptId.Value)) &&
			(!x.RecipientSnapshotId.HasValue || recipientIds.Contains(x.RecipientSnapshotId.Value)));
		return new(campaigns.Where(x => !x.IsDeleted), attempts, replies, campaignIds, recipientIds);
	}

	private async Task<GenericOperationalCounts> GetGenericOperationalCountsAsync()
	{
		using var archivedParents = DataFilter.Disable<ISoftDelete>();
		var queries = await GetGenericPayloadQueriesAsync(); var campaigns = queries.Campaigns;
		return new(await AsyncExecuter.CountAsync(campaigns),
			await AsyncExecuter.CountAsync(campaigns.Where(x => x.Status == CommunicationCampaignStatus.Draft)),
			await AsyncExecuter.CountAsync(campaigns.Where(x => x.Status == CommunicationCampaignStatus.PendingApproval)),
			await AsyncExecuter.CountAsync(campaigns.Where(x => x.Status == CommunicationCampaignStatus.Scheduled)),
			await AsyncExecuter.CountAsync(campaigns.Where(x => x.Status == CommunicationCampaignStatus.Queued || x.Status == CommunicationCampaignStatus.Sending)),
			await AsyncExecuter.CountAsync(campaigns.Where(x => x.Status == CommunicationCampaignStatus.Sent)),
			await AsyncExecuter.CountAsync(campaigns.Where(x => x.Status == CommunicationCampaignStatus.Failed || x.Status == CommunicationCampaignStatus.PartiallyFailed)),
			await AsyncExecuter.CountAsync(queries.Replies.Where(x => x.Status == CommunicationReplyStatus.Open || x.Status == CommunicationReplyStatus.Assigned)),
			await AsyncExecuter.CountAsync(queries.Attempts),
			await AsyncExecuter.CountAsync(queries.Attempts.Where(x => x.Status == CommunicationDeliveryStatus.Failed || x.Status == CommunicationDeliveryStatus.Bounced || x.Status == CommunicationDeliveryStatus.Skipped)));
	}

	private async Task EnsureGenericReplyReferencesAsync(Guid? campaignId, Guid? attemptId, Guid? recipientId)
	{
		using var archivedParents = DataFilter.Disable<ISoftDelete>();
		var queries = await GetGenericPayloadQueriesAsync();
		if (campaignId.HasValue && !await AsyncExecuter.AnyAsync(queries.CampaignIds.Where(x => x == campaignId.Value)) ||
			attemptId.HasValue && !await AsyncExecuter.AnyAsync(queries.Attempts.Where(x => x.Id == attemptId.Value)) ||
			recipientId.HasValue && !await AsyncExecuter.AnyAsync(queries.RecipientIds.Where(x => x == recipientId.Value)))
			throw new BusinessException(NotificationErrorCodes.UseWorkflow);
	}
}
