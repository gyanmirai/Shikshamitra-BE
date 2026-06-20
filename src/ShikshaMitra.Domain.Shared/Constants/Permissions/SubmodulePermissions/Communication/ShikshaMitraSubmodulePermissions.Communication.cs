using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Communication module permissions and submodules.
/// This file is split into multiple partial files for better maintainability:
/// - ShikshaMitraSubmodulePermissions.Communication.cs (Main module definition)
/// - ShikshaMitraSubmodulePermissions.Communication.Notification.cs
/// - ShikshaMitraSubmodulePermissions.Communication.Settings.cs
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Communication module permissions
	/// </summary>
	public static partial class Communication
	{
		/// <summary>
		/// Permission group name for Communication module.
		/// </summary>
		public const string GroupName = "Communication";

		/// <summary>
		/// Display name for Communication module permission group.
		/// </summary>
		public const string GroupDisplayName = "Permission:CommunicationModule";

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			// Core communication suite
			{ "Communication.Dashboard", new SubmoduleInfo(Dashboard.All, Dashboard.DisplayNames) },
			{ "Communication.Campaigns", new SubmoduleInfo(Campaigns.All, Campaigns.DisplayNames) },
			{ "Communication.Announcements", new SubmoduleInfo(Announcements.All, Announcements.DisplayNames) },
			{ "Communication.Inbox", new SubmoduleInfo(Inbox.All, Inbox.DisplayNames) },
			{ "Communication.History", new SubmoduleInfo(History.All, History.DisplayNames) },
			{ "Communication.Consent", new SubmoduleInfo(Consent.All, Consent.DisplayNames) },
			{ "Communication.UsageLedger", new SubmoduleInfo(UsageLedger.All, UsageLedger.DisplayNames) },

			// Notification submodules
			{ "Communication.Notification.SentNotification", new SubmoduleInfo(Notification_SentNotification.All, Notification_SentNotification.DisplayNames) },
			{ "Communication.Notification.Templates", new SubmoduleInfo(Notification_Templates.All, Notification_Templates.DisplayNames) },
			{ "Communication.Notification.NotificationSetting", new SubmoduleInfo(Notification_NotificationSetting.All, Notification_NotificationSetting.DisplayNames) },

			// Settings submodules
			{ "Communication.Settings.AnnouncementType", new SubmoduleInfo(Settings_AnnouncementType.All, Settings_AnnouncementType.DisplayNames) },
			{ "Communication.Settings.ProviderSettings", new SubmoduleInfo(Settings_ProviderSettings.All, Settings_ProviderSettings.DisplayNames) },
			{ "Communication.Settings.ApprovalRules", new SubmoduleInfo(Settings_ApprovalRules.All, Settings_ApprovalRules.DisplayNames) }
		};
	}
}
