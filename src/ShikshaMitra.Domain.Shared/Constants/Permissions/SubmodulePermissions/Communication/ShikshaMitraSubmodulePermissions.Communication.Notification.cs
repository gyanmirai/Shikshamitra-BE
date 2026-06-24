using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Communication
	{
		public static class Notification_SentNotification
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.SentNotification";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.SentNotification";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Send = $"{SubmoduleName}.Send";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Send, Approve, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Send, $"{SubmoduleDisplayName}.Send" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Notification_Templates
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.Templates";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.Templates";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Approve, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Notification_NotificationSetting
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.NotificationSetting";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.NotificationSetting";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
