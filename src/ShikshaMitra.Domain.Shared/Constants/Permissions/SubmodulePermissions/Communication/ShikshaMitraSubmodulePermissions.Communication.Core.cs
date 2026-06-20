using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Communication
	{
		public static class Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.Dashboard";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class Campaigns
		{
			private const string SubmoduleName = $"{ModuleName}.Campaigns";
			private const string SubmoduleDisplayName = $"{DisplayName}.Campaigns";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Send = $"{SubmoduleName}.Send";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Approve, Send, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Send, $"{SubmoduleDisplayName}.Send" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Announcements
		{
			private const string SubmoduleName = $"{ModuleName}.Announcements";
			private const string SubmoduleDisplayName = $"{DisplayName}.Announcements";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Approve = $"{SubmoduleName}.Approve";
			public const string Send = $"{SubmoduleName}.Send";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Approve, Send, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Approve, $"{SubmoduleDisplayName}.Approve" },
				{ Send, $"{SubmoduleDisplayName}.Send" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Inbox
		{
			private const string SubmoduleName = $"{ModuleName}.Inbox";
			private const string SubmoduleDisplayName = $"{DisplayName}.Inbox";

			public const string View = $"{SubmoduleName}.View";
			public const string Assign = $"{SubmoduleName}.Assign";

			public static readonly string[] All = { View, Assign };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Assign, $"{SubmoduleDisplayName}.Assign" }
			};
		}

		public static class History
		{
			private const string SubmoduleName = $"{ModuleName}.History";
			private const string SubmoduleDisplayName = $"{DisplayName}.History";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class Consent
		{
			private const string SubmoduleName = $"{ModuleName}.Consent";
			private const string SubmoduleDisplayName = $"{DisplayName}.Consent";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";

			public static readonly string[] All = { View, Add };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		public static class UsageLedger
		{
			private const string SubmoduleName = $"{ModuleName}.UsageLedger";
			private const string SubmoduleDisplayName = $"{DisplayName}.UsageLedger";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}
	}
}
