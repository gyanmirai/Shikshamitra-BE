using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationQueuePilotConfiguration_Tests
{
	[Theory]
	[InlineData("true")]
	[InlineData("false")]
	[InlineData(null)]
	public void A_configured_pilot_keeps_the_shared_worker_paused(string? enabled)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:QueueWorker:Enabled"] = enabled,
			["Notifications:QueueWorker:InAppPilot:TenantId"] = Guid.NewGuid().ToString()
		}).Build();

		NotificationQueueWorkerConfiguration.IsEnabled(configuration).ShouldBeFalse();
	}

	[Theory]
	[InlineData("false", true, true)]
	[InlineData("false", false, false)]
	[InlineData("true", true, false)]
	[InlineData("invalid", true, false)]
	[InlineData(null, true, false)]
	public void A_pilot_requires_explicit_shared_pause_and_every_nonempty_id(string? enabled, bool complete, bool expected)
	{
		var pilot = new NotificationInAppPilot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:QueueWorker:Enabled"] = enabled,
			["Notifications:QueueWorker:InAppPilot:TenantId"] = pilot.TenantId.ToString("N"),
			["Notifications:QueueWorker:InAppPilot:OperationId"] = pilot.OperationId.ToString(),
			["Notifications:QueueWorker:InAppPilot:RecipientUserId"] = (complete ? pilot.RecipientUserId : Guid.Empty).ToString()
		}).Build();

		NotificationQueueWorkerConfiguration.ReadInAppPilot(configuration).ShouldBe(expected ? pilot : null);
		NotificationQueueWorkerConfiguration.IsEnabled(configuration).ShouldBeFalse();
	}

	[Fact]
	public void The_shared_worker_can_run_when_explicitly_enabled_without_pilot_configuration()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:QueueWorker:Enabled"] = "true"
		}).Build();

		NotificationQueueWorkerConfiguration.IsEnabled(configuration).ShouldBeTrue();
		NotificationQueueWorkerConfiguration.ReadInAppPilot(configuration).ShouldBeNull();
	}
}
