using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Library
	{
		public static class Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.Dashboard";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class AllBooks
		{
			private const string SubmoduleName = $"{ModuleName}.AllBooks";
			private const string SubmoduleDisplayName = $"{DisplayName}.AllBooks";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class IssueReturn
		{
			private const string SubmoduleName = $"{ModuleName}.IssueReturn";
			private const string SubmoduleDisplayName = $"{DisplayName}.IssueReturn";

			public const string View = $"{SubmoduleName}.View";
			public const string Issue = $"{SubmoduleName}.Issue";
			public const string Return = $"{SubmoduleName}.Return";
			public const string Reserve = $"{SubmoduleName}.Reserve";

			public static readonly string[] All = { View, Issue, Return, Reserve };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Issue, $"{SubmoduleDisplayName}.Issue" },
				{ Return, $"{SubmoduleDisplayName}.Return" },
				{ Reserve, $"{SubmoduleDisplayName}.Reserve" }
			};
		}

		public static class LibraryMembers
		{
			private const string SubmoduleName = $"{ModuleName}.LibraryMembers";
			private const string SubmoduleDisplayName = $"{DisplayName}.LibraryMembers";

			public const string View = $"{SubmoduleName}.View";
			public const string Update = $"{SubmoduleName}.Update";

			public static readonly string[] All = { View, Update };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Update, $"{SubmoduleDisplayName}.Update" }
			};
		}

		public static class Charges
		{
			private const string SubmoduleName = $"{ModuleName}.Charges";
			private const string SubmoduleDisplayName = $"{DisplayName}.Charges";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Reports";

			public const string View = $"{SubmoduleName}.View";
			public const string Export = $"{SubmoduleName}.Export";

			public static readonly string[] All = { View, Export };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Export, $"{SubmoduleDisplayName}.Export" }
			};
		}

		public static class Notification
		{
			private const string SubmoduleName = $"{ModuleName}.Notification";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";

			public static readonly string[] All = { View, Add };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		public static class Settings
		{
			private const string SubmoduleName = $"{ModuleName}.Settings";
			private const string SubmoduleDisplayName = $"{DisplayName}.Settings";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}
	}
}
