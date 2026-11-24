using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace ShikshaMitra.Communication.Notifications;

public static class NotificationQueueWorkerConfiguration
{
	private const string SharedSwitch = "Notifications:QueueWorker:Enabled";
	private const string InAppPilotSection = "Notifications:QueueWorker:InAppPilot";
	private const string ExternalPilotSection = "Notifications:QueueWorker:ExternalPilot";
	private const string StagingInAppPilotSection = "Notifications:QueueWorker:StagingInAppPilot";
	private static readonly Guid DdvsTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private static readonly CommunicationChannelType[] QueueChannels =
		[CommunicationChannelType.InApp, CommunicationChannelType.Email, CommunicationChannelType.WhatsApp, CommunicationChannelType.WebPush];

	public static bool IsEnabled(IConfiguration configuration) =>
		!IsPilotConfigured(configuration) && IsTrue(configuration[SharedSwitch]);

	internal static bool IsPilotConfigured(IConfiguration configuration) =>
		IsTemplatePilotConfigured(configuration) || configuration.GetSection(StagingInAppPilotSection).Exists();

	private static bool IsTemplatePilotConfigured(IConfiguration configuration) =>
		configuration.GetSection(InAppPilotSection).Exists() || configuration.GetSection(ExternalPilotSection).Exists();

	public static StagingInAppNotificationPilot? ReadStagingInAppPilot(IConfiguration configuration, bool isStaging)
	{
		var section = configuration.GetSection(StagingInAppPilotSection);
		if (!isStaging || IsTemplatePilotConfigured(configuration) || !IsExplicitlyPaused(configuration) ||
			!TryReadId(section["TenantId"], out var tenantId) || tenantId != DdvsTenantId ||
			!TryReadId(section["OperationId"], out var operationId) || !TryReadId(section["SourceId"], out var sourceId) ||
			!TryReadId(section["RecipientUserId"], out var recipientUserId))
			return null;
		return new(tenantId, operationId, recipientUserId, sourceId);
	}

	public static NotificationTemplateTestPilot? ReadTemplateTestPilot(IConfiguration configuration)
	{
		if (configuration.GetSection(StagingInAppPilotSection).Exists()) return null;
		var inApp = configuration.GetSection(InAppPilotSection);
		var external = configuration.GetSection(ExternalPilotSection);
		var isExternal = external.Exists();
		var section = isExternal ? external : inApp;
		var channel = isExternal ? section["Channel"] switch
		{
			nameof(CommunicationChannelType.Email) => CommunicationChannelType.Email,
			nameof(CommunicationChannelType.WebPush) => CommunicationChannelType.WebPush,
			_ => (CommunicationChannelType?)null
		} : CommunicationChannelType.InApp;
		if (channel is null || isExternal && inApp.Exists() ||
			!IsExplicitlyPaused(configuration) ||
			!TryReadId(section["TenantId"], out var tenantId) ||
			!TryReadId(section["OperationId"], out var operationId) ||
			!TryReadId(section["RecipientUserId"], out var recipientUserId))
			return null;
		return new(tenantId, operationId, recipientUserId, channel.Value);
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
	private static bool IsExplicitlyPaused(IConfiguration configuration) =>
		bool.TryParse(configuration[SharedSwitch], out var enabled) && !enabled;
	private static bool TryReadId(string? value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;
	private static string SchoolKey(Guid tenantId) => $"Notifications:QueueWorker:Tenants:{tenantId:N}";
}
