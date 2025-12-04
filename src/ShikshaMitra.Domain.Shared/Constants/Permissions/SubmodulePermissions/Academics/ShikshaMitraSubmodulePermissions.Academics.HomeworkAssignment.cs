using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Academics module - Homework and Assignment related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		public static class HomeworkAndAssignment_LearningTasks
		{
			private const string SubmoduleName = $"{ModuleName}.HomeworkAndAssignment.LearningTasks";
			private const string SubmoduleDisplayName = $"{DisplayName}.HomeworkAndAssignment.LearningTasks";

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

		public static class HomeworkAndAssignment_AssessmentAndFeedback
		{
			private const string SubmoduleName = $"{ModuleName}.HomeworkAndAssignment.AssessmentAndFeedback";
			private const string SubmoduleDisplayName = $"{DisplayName}.HomeworkAndAssignment.AssessmentAndFeedback";

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

		public static class HomeworkAndAssignment_Reports
		{
			private const string SubmoduleName = $"{ModuleName}.HomeworkAndAssignment.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.HomeworkAndAssignment.Reports";

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

		public static class HomeworkAndAssignment_SettingsHomeworkTypes
		{
			private const string SubmoduleName = $"{ModuleName}.HomeworkAndAssignment.Settings.HomeworkTypes";
			private const string SubmoduleDisplayName = $"{DisplayName}.HomeworkAndAssignment.Settings.HomeworkTypes";

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

		public static class HomeworkAndAssignment_SettingsAssignmentTypes
		{
			private const string SubmoduleName = $"{ModuleName}.HomeworkAndAssignment.Settings.AssignmentTypes";
			private const string SubmoduleDisplayName = $"{DisplayName}.HomeworkAndAssignment.Settings.AssignmentTypes";

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
