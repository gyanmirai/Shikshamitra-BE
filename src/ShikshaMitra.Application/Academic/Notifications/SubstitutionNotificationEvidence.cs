using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class SubstitutionNotificationEvidence(IRepository<CommunicationCampaign, Guid> campaigns,
	IRepository<NotificationIntent, Guid> intents, IRepository<NotificationDispatch, Guid> dispatches,
	IRepository<CommunicationRecipientSnapshot, Guid> recipients, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<List<AcceptedNotice>> ReadAsync(Guid tenantId, Guid dutyId, CancellationToken token)
	{
		var source = SubstitutionNotificationSource.Type;
		return await queries.ToListAsync(
			from intent in await intents.GetQueryableAsync()
			join campaign in await campaigns.GetQueryableAsync() on intent.CampaignId equals (Guid?)campaign.Id
			join delivery in await dispatches.GetQueryableAsync() on intent.Id equals delivery.IntentId
			join recipient in await recipients.GetQueryableAsync() on delivery.RecipientSnapshotId equals (Guid?)recipient.Id
			where intent.TenantId == tenantId && campaign.TenantId == tenantId &&
				delivery.TenantId == tenantId && recipient.TenantId == tenantId &&
				!intent.IsDeleted && !campaign.IsDeleted && !delivery.IsDeleted && !recipient.IsDeleted &&
				intent.SourceId == dutyId && intent.SourceType == source &&
				campaign.SourceEntityId == dutyId && campaign.SourceEntityType == source &&
				campaign.CampaignType == NotificationCampaignTypes.Academic && campaign.SourceModule == CommunicationSourceModule.Academic &&
				campaign.Id == delivery.CampaignId && campaign.Id == recipient.CampaignId &&
				delivery.AcceptedAt.HasValue && recipient.UserId.HasValue && recipient.RecipientId.HasValue &&
				(intent.EventKey == SubstitutionNotificationSource.Assigned || intent.EventKey == SubstitutionNotificationSource.Changed ||
					intent.EventKey == SubstitutionNotificationSource.AcknowledgementDue)
			select new AcceptedNotice(recipient.RecipientType, recipient.UserId!.Value, recipient.RecipientId!.Value,
				intent.SourceRevision, delivery.AcceptedAt!.Value),
			token);
	}

	internal sealed record AcceptedNotice(string Role, Guid UserId, Guid RecipientId, string SourceRevision, DateTimeOffset AcceptedAt)
	{
		internal bool Matches(NotificationRecipientRefDto recipient) =>
			Role == recipient.Kind && UserId == recipient.UserId && RecipientId == (recipient.EmployeeId ?? recipient.UserId);
	}
}
