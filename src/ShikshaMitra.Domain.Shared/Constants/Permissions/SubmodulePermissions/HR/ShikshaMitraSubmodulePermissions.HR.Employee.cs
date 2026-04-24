using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// HR module - Employee related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class HR
	{
		// Employee - Employee List
		public static class Employee_EmployeeList
		{
			private const string SubmoduleName = $"{ModuleName}.Employee.EmployeeList";
			private const string SubmoduleDisplayName = $"{DisplayName}.Employee.EmployeeList";

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

		// Employee - ICard
		public static class Employee_ICard
		{
			private const string SubmoduleName = $"{ModuleName}.Employee.ICard";
			private const string SubmoduleDisplayName = $"{DisplayName}.Employee.ICard";

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

		// Employee - Setting - Document Types
		public static class Employee_SettingDocumentTypes
		{
			private const string SubmoduleName = $"{ModuleName}.Employee.Setting.DocumentTypes";
			private const string SubmoduleDisplayName = $"{DisplayName}.Employee.Setting.DocumentTypes";

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
