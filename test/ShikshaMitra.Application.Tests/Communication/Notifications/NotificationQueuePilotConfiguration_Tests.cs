using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationQueuePilotConfiguration_Tests
{
	[Fact]
	public void An_external_test_pilot_keeps_the_shared_worker_paused_even_with_conflicting_settings()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:QueueWorker:Enabled"] = "true",
			["Notifications:QueueWorker:ExternalPilot:Channel"] = "Email"
		}).Build();

		NotificationQueueWorkerConfiguration.IsEnabled(configuration).ShouldBeFalse();
	}

	[Theory]
	[InlineData("Email", true, false, CommunicationChannelType.Email)]
	[InlineData("WebPush", true, false, CommunicationChannelType.WebPush)]
	[InlineData("Email", false, false, null)]
	[InlineData("Email", true, true, null)]
	[InlineData("WhatsApp", true, false, null)]
	[InlineData("InApp", true, false, null)]
	[InlineData("2", true, false, null)]
	[InlineData(null, true, false, null)]
	public void External_pilot_requires_one_supported_channel_complete_ids_and_no_legacy_pilot(
		string? channel, bool complete, bool conflictingInApp, CommunicationChannelType? expected)
	{
		var school = Guid.NewGuid();
		var operation = Guid.NewGuid();
		var account = Guid.NewGuid();
		const string prefix = "Notifications:QueueWorker:ExternalPilot:";
		var settings = new Dictionary<string, string?> {
			["Notifications:QueueWorker:Enabled"] = "false",
			[prefix + "TenantId"] = school.ToString(),
			[prefix + "OperationId"] = operation.ToString(),
			[prefix + "RecipientUserId"] = (complete ? account : Guid.Empty).ToString(),
			[prefix + "Channel"] = channel
		};
		if (conflictingInApp) settings["Notifications:QueueWorker:InAppPilot:TenantId"] = school.ToString();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		NotificationQueueWorkerConfiguration.ReadTemplateTestPilot(configuration).ShouldBe(expected is { } allowed
			? new NotificationTemplateTestPilot(school, operation, account, allowed) : null);
		NotificationQueueWorkerConfiguration.IsEnabled(configuration).ShouldBeFalse();
	}

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
		var pilot = new NotificationTemplateTestPilot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:QueueWorker:Enabled"] = enabled,
			["Notifications:QueueWorker:InAppPilot:TenantId"] = pilot.TenantId.ToString("N"),
			["Notifications:QueueWorker:InAppPilot:OperationId"] = pilot.OperationId.ToString(),
			["Notifications:QueueWorker:InAppPilot:RecipientUserId"] = (complete ? pilot.RecipientUserId : Guid.Empty).ToString()
		}).Build();

		NotificationQueueWorkerConfiguration.ReadTemplateTestPilot(configuration).ShouldBe(expected ? pilot : null);
		NotificationQueueWorkerConfiguration.IsEnabled(configuration).ShouldBeFalse();
	}

	[Fact]
	public void The_shared_worker_can_run_when_explicitly_enabled_without_pilot_configuration()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:QueueWorker:Enabled"] = "true"
		}).Build();

		NotificationQueueWorkerConfiguration.IsEnabled(configuration).ShouldBeTrue();
		NotificationQueueWorkerConfiguration.ReadTemplateTestPilot(configuration).ShouldBeNull();
	}
}
