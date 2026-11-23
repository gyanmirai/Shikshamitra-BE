using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace ShikshaMitra.Communication.Notifications;

public static class NotificationQueueWorkerConfiguration
{
	private const string SharedSwitch = "Notifications:QueueWorker:Enabled";
	private const string PilotSection = "Notifications:QueueWorker:InAppPilot";

	private static readonly CommunicationChannelType[] QueueChannels =
		[CommunicationChannelType.InApp, CommunicationChannelType.Email, CommunicationChannelType.WhatsApp, CommunicationChannelType.WebPush];

	public static bool IsEnabled(IConfiguration configuration) =>
		!IsPilotConfigured(configuration) && IsTrue(configuration[SharedSwitch]);

	internal static bool IsPilotConfigured(IConfiguration configuration) => configuration.GetSection(PilotSection).Exists();

	public static NotificationInAppPilot? ReadInAppPilot(IConfiguration configuration)
	{
		var section = configuration.GetSection(PilotSection);
		if (!bool.TryParse(configuration[SharedSwitch], out var enabled) || enabled ||
			!TryReadId(section["TenantId"], out var tenantId) ||
			!TryReadId(section["OperationId"], out var operationId) ||
			!TryReadId(section["RecipientUserId"], out var recipientUserId))
			return null;
		return new(tenantId, operationId, recipientUserId);
	}

	public static bool IsSchoolPaused(IConfiguration configuration, Guid? tenantId) =>
		tenantId is Guid id && IsTrue(configuration[$"{SchoolKey(id)}:Paused"]);

	public static CommunicationChannelType[] ReadPausedChannels(IConfiguration configuration, Guid? tenantId)
	{
		if (tenantId is not Guid id) return [];
		var channels = configuration.GetSection($"{SchoolKey(id)}:Channels");
		return QueueChannels.Where(channel => IsTrue(channels[$"{channel}:Paused"])).ToArray();
	}

	private static bool IsTrue(string? value) => bool.TryParse(value, out var enabled) && enabled;
	private static bool TryReadId(string? value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;
	private static string SchoolKey(Guid tenantId) => $"Notifications:QueueWorker:Tenants:{tenantId:N}";
}
