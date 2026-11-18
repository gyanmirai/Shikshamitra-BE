using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(INotificationChannelSender))]
public sealed class WebPushNotificationChannelSender(ICurrentTenant tenant, IUnitOfWorkManager units, IClock clock,
	WebPushDispatchSubscriptions subscriptions, WebPushKeyConfiguration configuration, WebPushProviderClient provider,
	NotificationDispatchPayloadProtector payloads, WebPushSubscriptionProtector protector,
	IRepository<NotificationDispatch, Guid> dispatches, IRepository<IdentityUser, Guid> users, IAsyncQueryableExecuter queries,
	INotificationPersonalWriteLock writeLock) : INotificationChannelSender, ITransientDependency
{
	public CommunicationChannelType Channel => CommunicationChannelType.WebPush;

	public async Task<NotificationChannelSendResult> SendAsync(NotificationChannelSendRequest input, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (input.Channel != Channel || input.ProviderSettingId != Guid.Empty || input.Recipient?.UserId is not Guid userId || userId == Guid.Empty ||
			!Guid.TryParseExact(input.Destination, "N", out var subscriptionId) || subscriptionId == Guid.Empty)
			return Reject(NotificationErrorCodes.RecipientIneligible);
		var keys = configuration.Read();
		if (keys == null) return Reject(NotificationErrorCodes.ProviderUnavailable);

		ProtectedWebPushSubscription subscription;
		string subscriptionStamp;
		DateTimeOffset? expiresAt;
		using (var read = units.Begin(requiresNew: true, isTransactional: false))
		{
			var now = Now();
			var dispatch = await queries.FirstOrDefaultAsync((await dispatches.GetQueryableAsync()).Where(x =>
				x.TenantId == tenantId && x.Id == input.DispatchId && !x.IsDeleted && x.Channel == Channel &&
				x.TemplateRevisionId == input.TemplateRevisionId && x.DeduplicationKey == input.IdempotencyKey &&
				x.AttemptNumber == input.AttemptNumber && x.ProviderSettingId == null && x.Status == NotificationDispatchState.Processing &&
				x.IoStartedAt != null && x.LeaseId != null && x.LeaseId == x.IoLeaseId && x.LeaseExpiresAt > now), cancellationToken);
			if (dispatch == null) return Reject("WebPushDispatchUnavailable");
			expiresAt = dispatch.ExpiresAt;
			if (input.ExpiresAt.HasValue && (!expiresAt.HasValue || input.ExpiresAt < expiresAt)) expiresAt = input.ExpiresAt;
			if (expiresAt <= now) return Reject("SourceExpired");
			try
			{
				var payload = payloads.Unprotect(dispatch);
				if (payload.Recipient != input.Recipient || payload.Destination != input.Destination)
					return Reject(NotificationErrorCodes.RecipientIneligible);
				var active = await queries.AnyAsync((await users.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
					x.Id == userId && !x.IsDeleted && x.IsActive && (!x.LockoutEnabled || !x.LockoutEnd.HasValue || x.LockoutEnd <= now)),
					cancellationToken);
				if (!active) return Reject(NotificationErrorCodes.RecipientIneligible);
				var row = await subscriptions.FindAsync(userId, subscriptionId, keys.Version, cancellationToken);
				if (row == null) return Reject("WebPushSubscriptionUnavailable");
				subscription = protector.Unprotect(row);
				subscriptionStamp = row.ConcurrencyStamp;
			}
			catch (BusinessException error) when (error.Code is "DispatchPayloadInvalid" or NotificationErrorCodes.PushSubscriptionInvalid)
			{
				return Reject(error.Code!);
			}
			await read.CompleteAsync(cancellationToken);
		}

		var remaining = expiresAt - Now();
		if (remaining <= TimeSpan.Zero) return Reject("SourceExpired");
		var ttl = (int)Math.Min(remaining?.TotalSeconds ?? WebPushProviderClient.MaxTimeToLiveSeconds,
			WebPushProviderClient.MaxTimeToLiveSeconds);
		var result = await provider.SendAsync(subscription, keys, input.DispatchId, ttl, cancellationToken);
		if (result.ExpiredSubscription)
			await DisableIfUnchangedAsync(subscriptionId, userId, keys.Version, subscriptionStamp, cancellationToken);
		return result.Outcome;
	}

	private async Task DisableIfUnchangedAsync(Guid id, Guid userId, string version, string stamp, CancellationToken cancellationToken)
	{
		using var write = units.Begin(requiresNew: true, isTransactional: true);
		await writeLock.AcquireAsync(["push-subscriptions"], cancellationToken);
		var row = await subscriptions.FindAsync(userId, id, version, cancellationToken);
		if (row != null && row.ConcurrencyStamp == stamp)
		{
			row.Disable();
			await subscriptions.UpdateAsync(row, cancellationToken);
		}
		await write.CompleteAsync(cancellationToken);
	}

	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());

	private static NotificationChannelSendResult Reject(string code) => new(NotificationProviderAcceptance.Rejected, null, code);
}
