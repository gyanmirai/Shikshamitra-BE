using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class NotificationQueueHealthReporter(ICurrentTenant tenant, IUnitOfWorkManager units, IClock clock,
	INotificationQueueRepository queue, ILogger<NotificationQueueHealthReporter> logger) : ITransientDependency
{
	public async Task ReportAsync(CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		using var unit = units.Begin(requiresNew: true, isTransactional: false);
		var health = await queue.ReadHealthAsync(now, cancellationToken);
		await unit.CompleteAsync(cancellationToken);

		logger.LogInformation("AcademicNotificationQueueHealth TenantId={TenantId} DueIntents={DueIntents} OldestDueIntentSeconds={OldestDueIntentSeconds} " +
			"PendingWhatsAppReceipts={PendingWhatsAppReceipts} OldestWhatsAppReceiptSeconds={OldestWhatsAppReceiptSeconds}", tenantId, health.DueIntents,
			AgeSeconds(health.OldestDueIntentAt), health.PendingWhatsAppReceipts, AgeSeconds(health.OldestWhatsAppReceiptAt));
		foreach (var channel in health.Channels)
		{
			logger.LogInformation("AcademicNotificationChannelHealth TenantId={TenantId} Channel={Channel} Pending={Pending} Due={Due} " +
				"OldestDueSeconds={OldestDueSeconds} Processing={Processing} UnknownAcceptance={UnknownAcceptance} RecentFailures={RecentFailures} " +
				"PendingRetries={PendingRetries} PendingThrottled={PendingThrottled} RecentExpiredSubscriptions={RecentExpiredSubscriptions} " +
				"RecentConsentExclusions={RecentConsentExclusions} RecentSetupExclusions={RecentSetupExclusions}", tenantId, channel.Channel, channel.Pending, channel.Due,
				AgeSeconds(channel.OldestDueAt), channel.Processing, channel.UnknownAcceptance, channel.RecentFailures,
				channel.PendingRetries, channel.PendingThrottled, channel.RecentExpiredSubscriptions,
				channel.RecentConsentExclusions, channel.RecentSetupExclusions);
		}

		double? AgeSeconds(DateTimeOffset? oldest) => oldest.HasValue ? Math.Max(0, (now - oldest.Value).TotalSeconds) : null;
	}
}
