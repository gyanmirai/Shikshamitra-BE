using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Transport
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

		public static class StopManagement
		{
			private const string SubmoduleName = $"{ModuleName}.StopManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.StopManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = BuildCrudDisplayNames(SubmoduleName, SubmoduleDisplayName, All);
		}

		public static class RouteManagement
		{
			private const string SubmoduleName = $"{ModuleName}.RouteManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.RouteManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = BuildCrudDisplayNames(SubmoduleName, SubmoduleDisplayName, All);
		}

		public static class VehicleManagement
		{
			private const string SubmoduleName = $"{ModuleName}.VehicleManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.VehicleManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = BuildCrudDisplayNames(SubmoduleName, SubmoduleDisplayName, All);
		}

		public static class TransportStaffManagement
		{
			private const string SubmoduleName = $"{ModuleName}.TransportStaffManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.TransportStaffManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = BuildCrudDisplayNames(SubmoduleName, SubmoduleDisplayName, All);
		}

		public static class ApplicationManagement
		{
			private const string SubmoduleName = $"{ModuleName}.ApplicationManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.ApplicationManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = BuildCrudDisplayNames(SubmoduleName, SubmoduleDisplayName, All);
		}

		public static class Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Reports";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class Notification
		{
			private const string SubmoduleName = $"{ModuleName}.Notification";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = BuildCrudDisplayNames(SubmoduleName, SubmoduleDisplayName, All);
		}

		private static Dictionary<string, string> BuildCrudDisplayNames(string submoduleName, string submoduleDisplayName, string[] permissions)
		{
			var displayNames = new Dictionary<string, string> { { submoduleName, submoduleDisplayName } };
			foreach (var permission in permissions)
			{
				displayNames[permission] = $"{submoduleDisplayName}.{permission[(submoduleName.Length + 1)..]}";
			}

			return displayNames;
		}
	}
}
