using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Academics module - Curriculum Management related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		/// <summary>
		/// Curriculum Management - Subject Assignment permissions
		/// </summary>
		public static class CurriculumManagement_SubjectAssignment
		{
			private const string SubmoduleName = $"{ModuleName}.CurriculumManagement.SubjectAssignment";
			private const string SubmoduleDisplayName = $"{DisplayName}.CurriculumManagement.SubjectAssignment";

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

		/// <summary>
		/// Curriculum Management - Elective Assignment permissions
		/// </summary>
		public static class CurriculumManagement_ElectiveAssignment
		{
			private const string SubmoduleName = $"{ModuleName}.CurriculumManagement.ElectiveAssignment";
			private const string SubmoduleDisplayName = $"{DisplayName}.CurriculumManagement.ElectiveAssignment";

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

		/// <summary>
		/// Curriculum Management - Chapter Management permissions
		/// </summary>
		public static class CurriculumManagement_ChapterManagement
		{
			private const string SubmoduleName = $"{ModuleName}.CurriculumManagement.ChapterManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.CurriculumManagement.ChapterManagement";

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

		/// <summary>
		/// Curriculum Management - Lesson Plan / Teacher Diary permissions
		/// </summary>
		public static class CurriculumManagement_LessonPlan
		{
			private const string SubmoduleName = $"{ModuleName}.CurriculumManagement.LessonPlan";
			private const string SubmoduleDisplayName = $"{DisplayName}.CurriculumManagement.LessonPlan";

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

		/// <summary>
		/// Curriculum Management - Resources permissions
		/// </summary>
		public static class CurriculumManagement_Resources
		{
			private const string SubmoduleName = $"{ModuleName}.CurriculumManagement.Resources";
			private const string SubmoduleDisplayName = $"{DisplayName}.CurriculumManagement.Resources";

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

		/// <summary>
		/// Curriculum Management - Reports permissions
		/// </summary>
		public static class CurriculumManagement_Reports
		{
			private const string SubmoduleName = $"{ModuleName}.CurriculumManagement.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.CurriculumManagement.Reports";

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

		/// <summary>
		/// Curriculum Management - Settings - Subject Creation permissions
		/// </summary>
		public static class CurriculumManagement_SettingsSubjectCreation
		{
			private const string SubmoduleName = $"{ModuleName}.CurriculumManagement.Settings.SubjectCreation";
			private const string SubmoduleDisplayName = $"{DisplayName}.CurriculumManagement.Settings.SubjectCreation";

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

		/// <summary>
		/// Curriculum Management - Settings - Elective Group Creation permissions
		/// </summary>
		public static class CurriculumManagement_SettingsElectiveGroupCreation
		{
			private const string SubmoduleName = $"{ModuleName}.CurriculumManagement.Settings.ElectiveGroupCreation";
			private const string SubmoduleDisplayName = $"{DisplayName}.CurriculumManagement.Settings.ElectiveGroupCreation";

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
