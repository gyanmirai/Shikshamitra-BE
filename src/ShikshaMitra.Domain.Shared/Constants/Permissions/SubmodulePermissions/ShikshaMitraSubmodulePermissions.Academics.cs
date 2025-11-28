using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Academics module permissions and submodules
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Academics module permissions
	/// </summary>
	public static class Academics
	{
		/// <summary>
		/// Permission group name for Academics module.
		/// This creates a separate permission group in ABP for better organization.
		/// </summary>
		public const string GroupName = "Academics";
		
		/// <summary>
		/// Display name for Academics module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:AcademicsModule";

		private const string ModuleName = $"{ShikshaMitraSubmodulePermissions.GroupName}.Academics";
		private const string DisplayName = $"{DisplayNamePrefix}:Academics";

		public const string Default = ModuleName;

		// Curriculum Submodule
		public static class Curriculum
		{
			private const string SubmoduleName = $"{ModuleName}.Curriculum";
			private const string SubmoduleDisplayName = $"{DisplayName}.Curriculum";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Publish = $"{SubmoduleName}.Publish";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, Publish };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" }
			};
		}

		// Exams Submodule
		public static class Exams
		{
			private const string SubmoduleName = $"{ModuleName}.Exams";
			private const string SubmoduleDisplayName = $"{DisplayName}.Exams";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Schedule = $"{SubmoduleName}.Schedule";
			public const string GradeEntry = $"{SubmoduleName}.GradeEntry";
			public const string Publish = $"{SubmoduleName}.Publish";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, Schedule, GradeEntry, Publish };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Schedule, $"{SubmoduleDisplayName}.Schedule" },
				{ GradeEntry, $"{SubmoduleDisplayName}.GradeEntry" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" }
			};
		}

		// Timetable Submodule
		public static class Timetable
		{
			private const string SubmoduleName = $"{ModuleName}.Timetable";
			private const string SubmoduleDisplayName = $"{DisplayName}.Timetable";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Publish = $"{SubmoduleName}.Publish";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, Publish };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Publish, $"{SubmoduleDisplayName}.Publish" }
			};
		}

		// Assignments Submodule
		public static class Assignments
		{
			private const string SubmoduleName = $"{ModuleName}.Assignments";
			private const string SubmoduleDisplayName = $"{DisplayName}.Assignments";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Create = $"{SubmoduleName}.Create";
			public const string Edit = $"{SubmoduleName}.Edit";
			public const string Delete = $"{SubmoduleName}.Delete";
			public const string Grade = $"{SubmoduleName}.Grade";

			public static readonly string[] All = { Default, View, Create, Edit, Delete, Grade };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Create, $"{SubmoduleDisplayName}.Create" },
				{ Edit, $"{SubmoduleDisplayName}.Edit" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" },
				{ Grade, $"{SubmoduleDisplayName}.Grade" }
			};
		}

		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ Curriculum.Default, new SubmoduleInfo(Curriculum.All, Curriculum.DisplayNames) },
			{ Exams.Default, new SubmoduleInfo(Exams.All, Exams.DisplayNames) },
			{ Timetable.Default, new SubmoduleInfo(Timetable.All, Timetable.DisplayNames) },
			{ Assignments.Default, new SubmoduleInfo(Assignments.All, Assignments.DisplayNames) }
		};
	}
}
