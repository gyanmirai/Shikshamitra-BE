using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationAttemptReservation(Guid? AttemptId, DateTimeOffset? RetryAt, string? ExclusionCode);

[RemoteService(false), DisableAuditing]
public sealed class NotificationDeliveryProjection(ICurrentTenant tenant, INotificationPersonalWriteLock writeLock,
	NotificationSchoolTimeZone schoolZone, IRepository<ChannelProviderSetting, Guid> providers, IRepository<DeliveryAttempt, Guid> attempts,
	IAsyncQueryableExecuter queries, IGuidGenerator guids, IClock clock) : ITransientDependency
{
	public async Task<NotificationAttemptReservation> ReserveAsync(NotificationDispatch dispatch, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var tenantId = RequireTenant(dispatch);
		if (!dispatch.CampaignId.HasValue || !dispatch.RecipientSnapshotId.HasValue || dispatch.AttemptNumber < 1 ||
			dispatch.Status != NotificationDispatchState.Processing || dispatch.IoStartedAt.HasValue)
			throw new BusinessException(NotificationErrorCodes.StateConflict);
		var providerName = dispatch.Channel.ToString();
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		if (dispatch.ProviderSettingId is Guid providerId)
		{
			await writeLock.AcquireAsync([$"notification-provider-quota:{providerId:N}"], cancellationToken);
			var provider = await queries.FirstOrDefaultAsync((await providers.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
				x.Id == providerId && x.Channel == dispatch.Channel && !x.IsDeleted && x.IsEnabled && x.Status == CommunicationProviderStatus.Enabled)
				.Select(x => new { x.ProviderName, x.DailyQuota, x.MonthlyQuota }), cancellationToken);
			if (provider == null || dispatch.Channel == CommunicationChannelType.Email &&
				!string.Equals(provider.ProviderName.Trim(), "SMTP", StringComparison.OrdinalIgnoreCase))
				return new(null, null, NotificationErrorCodes.ProviderUnavailable);
			if (provider.DailyQuota <= 0 || provider.MonthlyQuota <= 0)
				return new(null, null, NotificationErrorCodes.ProviderQuotaExhausted);
			now = new DateTimeOffset(clock.Now.ToUniversalTime());
			if (provider.DailyQuota.HasValue || provider.MonthlyQuota.HasValue)
			{
				var retry = await NextQuotaWindowAsync(tenantId, providerId, now, provider.DailyQuota, provider.MonthlyQuota, cancellationToken);
				if (retry.HasValue) return new(null, retry, NotificationErrorCodes.ProviderQuotaExhausted);
			}
			providerName = provider.ProviderName;
		}
		else if (dispatch.Channel is CommunicationChannelType.Email or CommunicationChannelType.WhatsApp)
			return new(null, null, NotificationErrorCodes.ProviderUnavailable);

		var attempt = new DeliveryAttempt(guids.Create(), tenantId) {
			CampaignId = dispatch.CampaignId.Value, RecipientSnapshotId = dispatch.RecipientSnapshotId,
			NotificationDispatchId = dispatch.Id, NotificationProviderSettingId = dispatch.ProviderSettingId,
			Channel = dispatch.Channel, AttemptNumber = dispatch.AttemptNumber, QueuedAt = now,
			Destination = "Hidden", ProviderName = providerName
		};
		await attempts.InsertAsync(attempt, cancellationToken: cancellationToken);
		return new(attempt.Id, null, null);
	}

	public async Task RecordAsync(NotificationDispatch dispatch, DateTimeOffset at, CancellationToken cancellationToken)
	{
		var tenantId = RequireTenant(dispatch);
		if (dispatch.DeliveryAttemptId is not Guid attemptId) return;
		var attempt = await queries.FirstOrDefaultAsync((await attempts.GetQueryableAsync()).Where(x => x.Id == attemptId &&
			x.TenantId == tenantId && x.NotificationDispatchId == dispatch.Id && x.AttemptNumber == dispatch.AttemptNumber && !x.IsDeleted),
			cancellationToken) ?? throw new BusinessException(NotificationErrorCodes.StateConflict);
		attempt.Status = dispatch.Status switch
		{
			NotificationDispatchState.ProviderAccepted => CommunicationDeliveryStatus.Sent,
			NotificationDispatchState.Delivered => CommunicationDeliveryStatus.Delivered,
			NotificationDispatchState.Read => CommunicationDeliveryStatus.Read,
			NotificationDispatchState.UnknownAcceptance => CommunicationDeliveryStatus.UnknownAcceptance,
			NotificationDispatchState.Failed or NotificationDispatchState.Pending => CommunicationDeliveryStatus.Failed,
			_ => throw new BusinessException(NotificationErrorCodes.StateConflict)
		};
		var now = at.ToUniversalTime();
		attempt.ProviderMessageId = dispatch.ProviderMessageId;
		attempt.ErrorCode = dispatch.ErrorCode is { Length: <= 120 } ? dispatch.ErrorCode : null;
		attempt.SentAt ??= dispatch.AcceptedAt;
		if (attempt.Status is CommunicationDeliveryStatus.Delivered or CommunicationDeliveryStatus.Read) attempt.DeliveredAt ??= now;
		if (attempt.Status == CommunicationDeliveryStatus.Read) attempt.ReadAt ??= now;
		await attempts.UpdateAsync(attempt, cancellationToken: cancellationToken);
	}

	private async Task<DateTimeOffset?> NextQuotaWindowAsync(Guid tenantId, Guid providerId, DateTimeOffset now, int? dailyQuota,
		int? monthlyQuota, CancellationToken cancellationToken)
	{
		var zone = await schoolZone.GetAsync(cancellationToken);
		var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
		var month = new DateOnly(day.Year, day.Month, 1);
		var entries = (await attempts.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.NotificationProviderSettingId == providerId
			&& x.NotificationDispatchId != null && !x.IsDeleted && x.Status != CommunicationDeliveryStatus.Failed &&
			x.Status != CommunicationDeliveryStatus.Skipped);
		if (monthlyQuota.HasValue && await IsExhaustedAsync(entries, zone, month, month.AddMonths(1), monthlyQuota.Value, cancellationToken))
			return LocalMidnight(month.AddMonths(1), zone);
		if (dailyQuota.HasValue && await IsExhaustedAsync(entries, zone, day, day.AddDays(1), dailyQuota.Value, cancellationToken))
			return LocalMidnight(day.AddDays(1), zone);
		return null;
	}

	private async Task<bool> IsExhaustedAsync(IQueryable<DeliveryAttempt> entries, TimeZoneInfo zone, DateOnly from, DateOnly until,
		int limit, CancellationToken cancellationToken)
	{
		var start = LocalMidnight(from, zone);
		var end = LocalMidnight(until, zone);
		return await queries.LongCountAsync(entries.Where(x => x.QueuedAt >= start && x.QueuedAt < end), cancellationToken) >= limit;
	}

	private static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo zone) =>
		NotificationDeliveryTimingPolicy.Boundary(date.ToDateTime(TimeOnly.MinValue), zone, later: false);

	private Guid RequireTenant(NotificationDispatch dispatch) =>
		tenant.Id is Guid tenantId && tenantId != Guid.Empty && dispatch.TenantId == tenantId
			? tenantId : throw new BusinessException(NotificationErrorCodes.TenantRequired);
}
