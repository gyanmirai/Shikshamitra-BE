using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Academics module - Examination related permissions
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Academics
	{
		public static class Examination_ScheduleExam
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.ScheduleExam";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.ScheduleExam";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Examination_SeatAllocation
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.SeatAllocation";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.SeatAllocation";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { Default, View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class Examination_RecordMarks
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.RecordMarks";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.RecordMarks";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public static readonly string[] All = { Default, View };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" }
			};
		}

		public static class Examination_QuizManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.QuizManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.QuizManagement";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}

		public static class Examination_Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.Reports";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public static readonly string[] All = { Default, View, Add };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		public static class Examination_SettingsGradeScaling
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.Settings.GradeScaling";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.Settings.GradeScaling";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public static readonly string[] All = { Default, View, Add };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }
			};
		}

		public static class Examination_SettingsExamTypes
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.Settings.ExamTypes";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.Settings.ExamTypes";
			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";
			public const string Delete = $"{SubmoduleName}.Delete";
			public static readonly string[] All = { Default, View, Add, Update, Delete };
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName }, { View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" }, { Update, $"{SubmoduleDisplayName}.Update" },
				{ Delete, $"{SubmoduleDisplayName}.Delete" }
			};
		}
	}
}
