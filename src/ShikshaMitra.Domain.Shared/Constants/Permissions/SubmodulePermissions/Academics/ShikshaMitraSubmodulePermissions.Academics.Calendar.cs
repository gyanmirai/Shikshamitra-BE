using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		/// <summary>
		/// Academic Calendar permissions
		/// </summary>
		public static class Academics_AcademicCalendar
		{
			private const string SubmoduleName = $"{ModuleName}.AcademicCalendar";
			private const string SubmoduleDisplayName = $"{DisplayName}.AcademicCalendar";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AcademicCalendar_Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.AcademicCalendar.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.AcademicCalendar.Dashboard";

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class AcademicCalendar_CalendarManagement
		{
			private const string SubmoduleName = $"{ModuleName}.AcademicCalendar.CalendarManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.AcademicCalendar.CalendarManagement";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AcademicCalendar_EmergencyClosure
			{
				private const string SubmoduleName = $"{ModuleName}.AcademicCalendar.EmergencyClosure";
				private const string SubmoduleDisplayName = $"{DisplayName}.AcademicCalendar.EmergencyClosure";

				public const string View = $"{SubmoduleName}.View";
				public const string Add = $"{SubmoduleName}.Add";
				public const string Edit = $"{SubmoduleName}.Edit";
				public const string Delete = $"{SubmoduleName}.Delete";

				public static readonly string[] All = { View, Add, Edit, Delete };

				public static readonly Dictionary<string, string> DisplayNames = new()
				{
					{ View, $"{SubmoduleDisplayName}.View" },
					{ Add, $"{SubmoduleDisplayName}.Add" },
					{ Edit, $"{SubmoduleDisplayName}.Edit" },
					{ Delete, $"{SubmoduleDisplayName}.Delete" }
				};
			}

		public static class AcademicCalendar_ImpactLogs
		{
			private const string SubmoduleName = $"{ModuleName}.AcademicCalendar.ImpactLogs";
			private const string SubmoduleDisplayName = $"{DisplayName}.AcademicCalendar.ImpactLogs";

			public const string View = $"{SubmoduleName}.View";
			public const string Edit = $"{SubmoduleName}.Edit";

			public static readonly string[] All = { View, Edit };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" }
			};
		}

		public static class AcademicCalendar_AuditLogs
		{
			private const string SubmoduleName = $"{ModuleName}.AcademicCalendar.AuditLogs";
			private const string SubmoduleDisplayName = $"{DisplayName}.AcademicCalendar.AuditLogs";

			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Edit, Delete };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class AcademicCalendar_Settings
		{
			private const string SubmoduleName = $"{ModuleName}.AcademicCalendar.Settings";
			private const string SubmoduleDisplayName = $"{DisplayName}.AcademicCalendar.Settings";

			public const string View = $"{SubmoduleName}.View";
			public const string Edit = $"{SubmoduleName}.Edit";

			public static readonly string[] All = { View, Edit };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" }
			};
		}
	}
}
