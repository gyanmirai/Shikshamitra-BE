using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// ClassManagement module permissions and submodules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// ClassManagement module permissions
	/// </summary>
	public static class ClassManagement
	{
		/// <summary>
		/// Permission group name for ClassManagement module
		/// </summary>
		public const string GroupName = "ClassManagement";

		/// <summary>
		/// Display name for ClassManagement module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:ClassManagementModule";

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		// Dashboard
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

		// Student Elective Assignment
		public static class StudentElectiveAssignment
		{
			private const string SubmoduleName = $"{ModuleName}.StudentElectiveAssignment";
			private const string SubmoduleDisplayName = $"{DisplayName}.StudentElectiveAssignment";
			
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

		// Chapters Progress
		public static class ChaptersProgress
		{
			private const string SubmoduleName = $"{ModuleName}.ChaptersProgress";
			private const string SubmoduleDisplayName = $"{DisplayName}.ChaptersProgress";
			
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

		// Teachers Assignment
		public static class TeachersAssignment
		{
			private const string SubmoduleName = $"{ModuleName}.TeachersAssignment";
			private const string SubmoduleDisplayName = $"{DisplayName}.TeachersAssignment";
			
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

		// Class Teacher Assignment
		public static class ClassTeacherAssignment
		{
			private const string SubmoduleName = $"{ModuleName}.ClassTeacherAssignment";
			private const string SubmoduleDisplayName = $"{DisplayName}.ClassTeacherAssignment";
			
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

		// Notification - Student Elective Assignment
		public static class Notification_StudentElectiveAssignment
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.StudentElectiveAssignment";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.StudentElectiveAssignment";
			
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

		// Notification - Chapter Progress
		public static class Notification_ChapterProgress
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.ChapterProgress";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.ChapterProgress";
			
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

		// Notification - Teachers Assignment
		public static class Notification_TeachersAssignment
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.TeachersAssignment";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.TeachersAssignment";
			
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

		// Notification - Class Teacher Assignment
		public static class Notification_ClassTeacherAssignment
		{
			private const string SubmoduleName = $"{ModuleName}.Notification.ClassTeacherAssignment";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification.ClassTeacherAssignment";
			
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

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ "ClassManagement.Dashboard", new SubmoduleInfo(Dashboard.All, Dashboard.DisplayNames) },
			{ "ClassManagement.StudentElectiveAssignment", new SubmoduleInfo(StudentElectiveAssignment.All, StudentElectiveAssignment.DisplayNames) },
			{ "ClassManagement.ChaptersProgress", new SubmoduleInfo(ChaptersProgress.All, ChaptersProgress.DisplayNames) },
			{ "ClassManagement.TeachersAssignment", new SubmoduleInfo(TeachersAssignment.All, TeachersAssignment.DisplayNames) },
			{ "ClassManagement.ClassTeacherAssignment", new SubmoduleInfo(ClassTeacherAssignment.All, ClassTeacherAssignment.DisplayNames) },
			{ "ClassManagement.Notification.StudentElectiveAssignment", new SubmoduleInfo(Notification_StudentElectiveAssignment.All, Notification_StudentElectiveAssignment.DisplayNames) },
			{ "ClassManagement.Notification.ChapterProgress", new SubmoduleInfo(Notification_ChapterProgress.All, Notification_ChapterProgress.DisplayNames) },
			{ "ClassManagement.Notification.TeachersAssignment", new SubmoduleInfo(Notification_TeachersAssignment.All, Notification_TeachersAssignment.DisplayNames) },
			{ "ClassManagement.Notification.ClassTeacherAssignment", new SubmoduleInfo(Notification_ClassTeacherAssignment.All, Notification_ClassTeacherAssignment.DisplayNames) }
		};
	}
}
