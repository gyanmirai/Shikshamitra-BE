using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(INotificationChannelSender))]
public sealed class InAppNotificationChannelSender(ICurrentTenant tenant, IUnitOfWorkManager units, IClock clock,
	NotificationOutcomeLock outcomeLock, IRepository<NotificationDispatch, Guid> dispatches, IRepository<NotificationIntent, Guid> intents,
	IRepository<NotificationInboxItem, Guid> inbox, IRepository<IdentityUser, Guid> users, IAsyncQueryableExecuter queries,
	IGuidGenerator guids, NotificationTemplateVariableValidator validator, NotificationDeliveryProjection delivery)
	: INotificationChannelSender, ITransientDependency
{
	public CommunicationChannelType Channel => CommunicationChannelType.InApp;

	public async Task<NotificationChannelSendResult> SendAsync(NotificationChannelSendRequest input, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		var recipient = input.Recipient;
		if (input.Channel != Channel || recipient?.UserId is not Guid userId || userId == Guid.Empty ||
			!string.Equals(input.Destination, userId.ToString("N"), StringComparison.Ordinal))
			return Reject(NotificationErrorCodes.RecipientIneligible);
		var title = string.IsNullOrWhiteSpace(input.Subject) ? "Academic update" : input.Subject;
		var relativeLink = RelativeLink(input.LinkUrl);
		if (relativeLink == null || !NotificationTemplateVariableValidator.WithinLimits(Channel, title, input.Body))
			return Reject(NotificationErrorCodes.TemplateInvalid);

		using var unit = units.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.ReadCommitted);
		var query = (await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.Id == input.DispatchId &&
			!x.IsDeleted && x.Channel == Channel && x.AttemptNumber == input.AttemptNumber && x.DeduplicationKey == input.IdempotencyKey &&
			x.TemplateRevisionId == input.TemplateRevisionId && x.IoStartedAt != null);
		var operationId = await queries.FirstOrDefaultAsync(query.Select(x => x.CampaignId), cancellationToken);
		if (!operationId.HasValue) return Reject("InboxDispatchUnavailable");
		await outcomeLock.AcquireAsync(operationId.Value, cancellationToken);
		var dispatch = await queries.FirstOrDefaultAsync(query.Where(x => x.CampaignId == operationId), cancellationToken);
		if (dispatch == null || !MatchesRecipient(dispatch, recipient)) return Reject(NotificationErrorCodes.RecipientIneligible);
		if (dispatch.Status is NotificationDispatchState.Delivered or NotificationDispatchState.Read && dispatch.ProviderMessageId is { } receipt)
			return new(NotificationProviderAcceptance.Accepted, receipt, null);

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		if (dispatch.Status != NotificationDispatchState.Processing || dispatch.IoLeaseId is not Guid lease || dispatch.LeaseId != lease ||
			!dispatch.LeaseExpiresAt.HasValue || dispatch.LeaseExpiresAt <= now || dispatch.ExpiresAt <= now)
			return Reject("InboxDispatchUnavailable");

		var intent = await queries.FirstOrDefaultAsync((await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Id == dispatch.IntentId && !x.IsDeleted && x.Status == NotificationIntentState.AwaitingDelivery &&
			x.PlanningCompletedAt != null && x.CampaignId == operationId && (!x.ExpiresAt.HasValue || x.ExpiresAt > now))
			.Select(x => new { x.SourceId, x.SourceType }), cancellationToken);
		if (intent == null) return Reject(NotificationErrorCodes.SourceUnavailable);
		var active = await queries.AnyAsync((await users.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.Id == userId &&
			!x.IsDeleted && x.IsActive && (!x.LockoutEnabled || !x.LockoutEnd.HasValue || x.LockoutEnd <= now)), cancellationToken);
		if (!active) return Reject(NotificationErrorCodes.RecipientIneligible);

		var item = new NotificationInboxItem(guids.Create(), tenantId, userId, dispatch.Id, intent.SourceType, intent.SourceId,
			dispatch.RecipientContextKey, dispatch.DeduplicationKey, title, input.Body, relativeLink, recipient.StudentId);
		var providerId = $"inbox:{item.Id:N}";
		dispatch.RecordOutcome(lease, now, NotificationDispatchState.Delivered, providerId);
		await inbox.InsertAsync(item, cancellationToken: cancellationToken);
		await delivery.RecordAsync(dispatch, now, cancellationToken);
		await dispatches.UpdateAsync(dispatch, cancellationToken: cancellationToken);
		await unit.CompleteAsync(cancellationToken);
		return new(NotificationProviderAcceptance.Accepted, providerId, null);
	}

	private string? RelativeLink(string? link)
	{
		if (!validator.IsAllowedLink(link) || !Uri.TryCreate(link, UriKind.Absolute, out var address)) return null;
		var relative = address.PathAndQuery + address.Fragment;
		return relative.Length <= NotificationInboxItem.MaximumLinkLength && relative.StartsWith('/') &&
			!relative.StartsWith("//", StringComparison.Ordinal) ? relative : null;
	}

	private static bool MatchesRecipient(NotificationDispatch dispatch, NotificationRecipientRefDto recipient)
	{
		try
		{
			return NotificationRuntimeJson.Deserialize<NotificationRecipientRefDto>(dispatch.RecipientReferenceJson,
				NotificationErrorCodes.RuntimeSnapshotInvalid) == recipient;
		}
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuntimeSnapshotInvalid)
		{
			return false;
		}
	}

	private static NotificationChannelSendResult Reject(string code) => new(NotificationProviderAcceptance.Rejected, null, code);
}
