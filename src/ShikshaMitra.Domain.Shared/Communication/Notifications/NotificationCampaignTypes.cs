using System;

namespace ShikshaMitra.Communication.Notifications;

public static class NotificationCampaignTypes
{
	public const string Academic = "AcademicNotification";
	public const string AcademicNormalized = "academicnotification";
	public static bool IsAcademic(string? value) => string.Equals(value?.Trim(), Academic, StringComparison.OrdinalIgnoreCase);
}
