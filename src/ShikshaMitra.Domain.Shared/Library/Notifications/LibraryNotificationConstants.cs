namespace ShikshaMitra.Library.Notifications;

public static class LibraryNotificationEventGroups
{
	public const string BookManagement = "BookManagement";
	public const string MemberManagement = "MemberManagement";
	public const string Transactions = "Transactions";
}

public static class LibraryNotificationRecipientRoles
{
	public const string Member = "Member";
	public const string Librarian = "Librarian";
}

public static class LibraryNotificationChannels
{
	public const string InApp = "InApp";
	public const string Email = "Email";
	public const string Sms = "Sms";
	public const string WhatsApp = "WhatsApp";
}

public static class LibraryNotificationDeliveryStatuses
{
	public const string Queued = "Queued";
	public const string Processing = "Processing";
	public const string Unconfirmed = "Unconfirmed";
	public const string Sent = "Sent";
	public const string Skipped = "Skipped";
	public const string Failed = "Failed";
}

public static class LibraryNotificationErrorCodes
{
	public const string RetryNotAllowed = "Library:Notifications:011001";
	public const string DuplicateAcknowledgementRequired = "Library:Notifications:011002";
	public const string RecipientAccountRequired = "Library:Notifications:010070";
	public const string ConfigurationInvalid = "Library:Notifications:010071";
}

public static class LibraryNotificationSettingNames
{
	public const string AutomaticReminderTimeLocal = "ShikshaMitra.Library.Notifications.AutomaticReminderTimeLocal";
	public const string OperationalRecipientUserIds = "ShikshaMitra.Library.Notifications.OperationalRecipientUserIds";
	public const string StudentInboxRouting = "ShikshaMitra.Library.Notifications.StudentInboxRouting";
}

public static class LibraryStudentInboxRouting
{
	public const string Guardians = "Guardians";
	public const string StudentAndGuardians = "StudentAndGuardians";
	public const string Student = "Student";
}

public static class LibraryNotificationEventTypes
{
	public const string BookIssueConfirmation = "bookIssueConfirmation";
	public const string DueDateReminder = "dueDateReminder";
	public const string OverdueReminder = "overdueReminder";
	public const string ReturnConfirmation = "returnConfirmation";
	public const string ReturnChargeReminder = "returnChargeReminder";
	public const string ReservationConfirmed = "reservationConfirmed";
	public const string ReservedBookAvailable = "reservedBookAvailable";
	public const string ReservationExpiring = "reservationExpiring";
	public const string FineDueReminder = "fineDueReminder";
	public const string LowStockAlert = "lowStockAlert";
	public const string MembershipExpiring = "membershipExpiring";
	public const string MembershipExpired = "membershipExpired";
	public const string MembershipReactivated = "membershipReactivated";
}
