using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		public static class LearningManagementSystem_Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.Dashboard";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";

			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_Course
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.Course";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.Course";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_StudentProgressTracking
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.StudentProgressTracking";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.StudentProgressTracking";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_ContentLibrary
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.ContentLibrary";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.ContentLibrary";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_ReportsAndAnalytics
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.ReportsAndAnalytics";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.ReportsAndAnalytics";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_SettingsGeneral
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.Settings.General";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.Settings.General";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_SettingsCourseManagement
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.Settings.CourseManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.Settings.CourseManagement";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_SettingsContentManagement
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.Settings.ContentManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.Settings.ContentManagement";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_SettingsNotification
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.Settings.Notification";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.Settings.Notification";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class LearningManagementSystem_SettingsIntegration
		{
			private const string SubmoduleName = $"{ModuleName}.LearningManagementSystem.Settings.Integration";
			private const string SubmoduleDisplayName = $"{DisplayName}.LearningManagementSystem.Settings.Integration";
			
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Delete = $"{SubmoduleName}.Delete";
			
			public static readonly string[] All = { View, Add, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
