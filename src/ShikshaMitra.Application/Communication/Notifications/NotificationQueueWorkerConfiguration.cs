using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace ShikshaMitra.Communication.Notifications;

public static class NotificationQueueWorkerConfiguration
{
	private static readonly CommunicationChannelType[] QueueChannels =
		[CommunicationChannelType.InApp, CommunicationChannelType.Email, CommunicationChannelType.WhatsApp, CommunicationChannelType.WebPush];

	public static bool IsEnabled(IConfiguration configuration) =>
		IsTrue(configuration["Notifications:QueueWorker:Enabled"]);

	public static bool IsSchoolPaused(IConfiguration configuration, Guid? tenantId) =>
		tenantId is Guid id && IsTrue(configuration[$"{SchoolKey(id)}:Paused"]);

	public static CommunicationChannelType[] ReadPausedChannels(IConfiguration configuration, Guid? tenantId)
	{
		if (tenantId is not Guid id) return [];
		var channels = configuration.GetSection($"{SchoolKey(id)}:Channels");
		return QueueChannels.Where(channel => IsTrue(channels[$"{channel}:Paused"])).ToArray();
	}

	private static bool IsTrue(string? value) => bool.TryParse(value, out var enabled) && enabled;
	private static string SchoolKey(Guid tenantId) => $"Notifications:QueueWorker:Tenants:{tenantId:N}";
}
