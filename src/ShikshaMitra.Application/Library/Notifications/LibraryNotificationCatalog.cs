using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationCatalog : ISingletonDependency
{
	private static readonly IReadOnlyList<LibraryNotificationDefinition> Definitions =
	[
		new(
			LibraryNotificationEventTypes.BookIssueConfirmation,
			LibraryNotificationEventGroups.Transactions,
			"Book issue confirmation",
			LibraryNotificationRecipientRoles.Member,
			false,
			null,
			"Book issued: {{bookTitle}}",
			"Dear {{memberName}}, {{bookTitle}} ({{accessionNumber}}) was issued on {{issueDate}} and is due on {{dueDate}}."),
		new(
			LibraryNotificationEventTypes.DueDateReminder,
			LibraryNotificationEventGroups.Transactions,
			"Due-date reminder",
			LibraryNotificationRecipientRoles.Member,
			false,
			2,
			"Library book due soon: {{bookTitle}}",
			"Dear {{memberName}}, {{bookTitle}} ({{accessionNumber}}) is due on {{dueDate}}. Please return or renew it on time."),
		new(
			LibraryNotificationEventTypes.OverdueReminder,
			LibraryNotificationEventGroups.Transactions,
			"Overdue reminder",
			LibraryNotificationRecipientRoles.Member,
			false,
			null,
			"Library book overdue: {{bookTitle}}",
			"Dear {{memberName}}, {{bookTitle}} ({{accessionNumber}}) was due on {{dueDate}}. Please return it at the earliest."),
		new(
			LibraryNotificationEventTypes.ReturnConfirmation,
			LibraryNotificationEventGroups.Transactions,
			"Return confirmation",
			LibraryNotificationRecipientRoles.Member,
			false,
			null,
			"Book returned: {{bookTitle}}",
			"Dear {{memberName}}, your return of {{bookTitle}} ({{accessionNumber}}) was recorded on {{returnDate}}."),
		new(
			LibraryNotificationEventTypes.ReturnChargeReminder,
			LibraryNotificationEventGroups.Transactions,
			"Late/damaged/lost return charge reminder",
			LibraryNotificationRecipientRoles.Member,
			false,
			null,
			"Library return charge due",
			"Dear {{memberName}}, a Library charge of {{amount}} for {{bookTitle}} is due on {{fineDueDate}}."),
		new(
			LibraryNotificationEventTypes.ReservationConfirmed,
			LibraryNotificationEventGroups.Transactions,
			"Reservation confirmed",
			LibraryNotificationRecipientRoles.Member,
			false,
			null,
			"Library reservation confirmed: {{bookTitle}}",
			"Dear {{memberName}}, your reservation for {{bookTitle}} is confirmed until {{reserveDueDate}}."),
		new(
			LibraryNotificationEventTypes.ReservedBookAvailable,
			LibraryNotificationEventGroups.BookManagement,
			"Reserved book available",
			LibraryNotificationRecipientRoles.Member,
			false,
			null,
			"Reserved book available: {{bookTitle}}",
			"Dear {{memberName}}, {{bookTitle}} is now available for your reservation. Please collect it by {{reserveDueDate}}."),
		new(
			LibraryNotificationEventTypes.ReservationExpiring,
			LibraryNotificationEventGroups.Transactions,
			"Reservation expiring/expired",
			LibraryNotificationRecipientRoles.Member,
			false,
			1,
			"Library reservation expiring: {{bookTitle}}",
			"Dear {{memberName}}, your reservation for {{bookTitle}} expires on {{reserveDueDate}}."),
		new(
			LibraryNotificationEventTypes.FineDueReminder,
			LibraryNotificationEventGroups.MemberManagement,
			"Fine due reminder",
			LibraryNotificationRecipientRoles.Member,
			false,
			null,
			"Library fine due",
			"Dear {{memberName}}, your Library fine of {{amount}} is due on {{fineDueDate}}."),
		new(
			LibraryNotificationEventTypes.LowStockAlert,
			LibraryNotificationEventGroups.BookManagement,
			"Low stock alert",
			LibraryNotificationRecipientRoles.Librarian,
			false,
			null,
			"Library low stock alert: {{bookTitle}}",
			"{{bookTitle}} has only {{availableCopies}} available copy/copies out of {{totalCopies}}."),
		new(
			LibraryNotificationEventTypes.MembershipExpiring,
			LibraryNotificationEventGroups.MemberManagement,
			"Membership expiring",
			LibraryNotificationRecipientRoles.Member,
			true,
			null,
			"Library membership expiring",
			"Membership expiry notifications are coming soon."),
		new(
			LibraryNotificationEventTypes.MembershipExpired,
			LibraryNotificationEventGroups.MemberManagement,
			"Membership expired",
			LibraryNotificationRecipientRoles.Member,
			true,
			null,
			"Library membership expired",
			"Membership expiry notifications are coming soon."),
		new(
			LibraryNotificationEventTypes.MembershipReactivated,
			LibraryNotificationEventGroups.MemberManagement,
			"Membership reactivated",
			LibraryNotificationRecipientRoles.Member,
			true,
			null,
			"Library membership reactivated",
			"Membership reactivation notifications are coming soon.")
	];

	private static readonly IReadOnlyList<LibraryNotificationChannelDefinition> Channels =
	[
		new(LibraryNotificationChannels.InApp, "In-App", true),
		new(LibraryNotificationChannels.Email, "Email", true),
		new(LibraryNotificationChannels.Sms, "SMS", false),
		new(LibraryNotificationChannels.WhatsApp, "WhatsApp", false)
	];

	public IReadOnlyList<LibraryNotificationDefinition> GetDefinitions() => Definitions;

	public IReadOnlyList<LibraryNotificationChannelDefinition> GetChannels() => Channels;

	public LibraryNotificationDefinition Resolve(string eventType) =>
		Definitions.FirstOrDefault(x => x.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase))
		?? throw new Volo.Abp.BusinessException(code: LibraryErrorCodes.NotificationEventNotSupported, message: "Library notification event type is not supported.");

	public List<LibraryNotificationSetting> BuildDefaultSettings(Func<Guid> idFactory, Guid? tenantId)
	{
		return Definitions
			.SelectMany(definition => Channels.Select(channel =>
				new LibraryNotificationSetting(
					idFactory(),
					tenantId,
					definition.EventType,
					definition.EventGroup,
					definition.EventName,
					definition.RecipientRole,
					channel.Channel,
					isActive: !definition.IsComingSoon && channel.IsAvailable,
					isChannelAvailable: channel.IsAvailable,
					isComingSoon: definition.IsComingSoon,
					definition.ReminderOffsetDays,
					definition.SubjectTemplate,
					definition.MessageTemplate)))
			.ToList();
	}
}

public sealed record LibraryNotificationDefinition(
	string EventType,
	string EventGroup,
	string EventName,
	string RecipientRole,
	bool IsComingSoon,
	int? ReminderOffsetDays,
	string SubjectTemplate,
	string MessageTemplate);

public sealed record LibraryNotificationChannelDefinition(string Channel, string Label, bool IsAvailable);
