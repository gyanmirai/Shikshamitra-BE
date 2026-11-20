using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public sealed class NotificationQueueHealthReporter_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly IUnitOfWorkManager _units = Substitute.For<IUnitOfWorkManager>();
	private readonly INotificationQueueRepository _queue = Substitute.For<INotificationQueueRepository>();
	private readonly ILogger<NotificationQueueHealthReporter> _logger = Substitute.For<ILogger<NotificationQueueHealthReporter>>();

	[Fact]
	public async Task Logs_tenant_and_channel_aggregates_with_queue_age_in_seconds()
	{
		var school = Guid.NewGuid();
		_tenant.Id.Returns(school);
		var unit = Substitute.For<IUnitOfWork>();
		_units.Begin(Arg.Is<AbpUnitOfWorkOptions>(x => !x.IsTransactional), true).Returns(unit);
		_queue.ReadHealthAsync(Now, default).Returns(new NotificationQueueHealth(2, Now.AddMinutes(-3),
			[new(CommunicationChannelType.WhatsApp, 3, 1, Now.AddMinutes(-4), 2, 1, 1, 2, 1, 0, 1, 0)],
			4, Now.AddMinutes(-5)));

		await Reporter().ReportAsync(default);

		await unit.Received(1).CompleteAsync(default);
		var events = _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log))
			.Select(call => ((IEnumerable<KeyValuePair<string, object?>>)call.GetArguments()[2]!).ToDictionary())
			.ToArray();
		events.Length.ShouldBe(2);
		events[0]["TenantId"].ShouldBe(school);
		events[0]["OldestDueIntentSeconds"].ShouldBe(180d);
		events[0]["OldestWhatsAppReceiptSeconds"].ShouldBe(300d);
		events[1]["TenantId"].ShouldBe(school);
		events[1]["Channel"].ShouldBe(CommunicationChannelType.WhatsApp);
		events[1]["OldestDueSeconds"].ShouldBe(240d);
		events[1]["UnknownAcceptance"].ShouldBe(1L);
		events[1].Keys.ShouldBe(["TenantId", "Channel", "Pending", "Due", "OldestDueSeconds", "Processing",
			"UnknownAcceptance", "RecentFailures", "PendingRetries", "PendingThrottled", "RecentExpiredSubscriptions",
			"RecentConsentExclusions", "RecentSetupExclusions", "{OriginalFormat}"], ignoreOrder: true);
	}

	[Fact]
	public async Task Refuses_to_read_health_without_an_active_tenant_context()
	{
		(await Should.ThrowAsync<BusinessException>(() => Reporter().ReportAsync(default))).Code
			.ShouldBe(NotificationErrorCodes.TenantRequired);
		await _queue.DidNotReceive().ReadHealthAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	private NotificationQueueHealthReporter Reporter()
	{
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(Now.UtcDateTime);
		return new(_tenant, _units, clock, _queue, _logger);
	}
}
