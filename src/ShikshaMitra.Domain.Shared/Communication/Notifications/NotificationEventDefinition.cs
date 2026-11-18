using System.Collections.Generic;

namespace ShikshaMitra.Communication.Notifications;

public enum NotificationTriggerType { Change, Due, Manual }
public enum NotificationDeliveryProfile { Routine, Important, Internal, Digest, Private, Manual }
public enum NotificationVariableType { Text, Date, DateTime, Number, Link }
public enum NotificationVariableSensitivity { Public, Personal, AuthenticatedLink }
public enum NotificationGuardianMode { PrimaryOnly, PrimaryAndSelected }

public sealed record NotificationVariableDefinition(string Key, NotificationVariableType Type,
	bool Required, NotificationVariableSensitivity Sensitivity);

public sealed record NotificationEventDefinition(
	string EventKey,
	string Category,
	string SourceType,
	string LinkBuilderKey,
	int SchemaVersion,
	NotificationTriggerType TriggerType,
	NotificationDeliveryProfile DeliveryProfile,
	IReadOnlyList<string> RecipientSelectors,
	IReadOnlyList<CommunicationChannelType> SupportedChannels,
	IReadOnlyList<NotificationVariableDefinition> Variables,
	IReadOnlyList<string> MaterialChangeFields);

public sealed record NotificationSourceAvailability(bool IsAvailable, string? ReasonCode);
