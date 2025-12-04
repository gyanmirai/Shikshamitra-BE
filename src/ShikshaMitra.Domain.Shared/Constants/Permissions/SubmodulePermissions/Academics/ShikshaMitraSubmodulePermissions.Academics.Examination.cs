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

		public static class Examination_SeatAllocation
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.SeatAllocation";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.SeatAllocation";
			
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

		public static class Examination_RecordMarks
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.RecordMarks";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.RecordMarks";
			
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

		public static class Examination_QuizManagement
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.QuizManagement";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.QuizManagement";
			
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

		public static class Examination_Reports
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.Reports";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.Reports";
			
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

		public static class Examination_SettingsGradeScaling
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.Settings.GradeScaling";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.Settings.GradeScaling";
			
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

		public static class Examination_SettingsExamTypes
		{
			private const string SubmoduleName = $"{ModuleName}.Examination.Settings.ExamTypes";
			private const string SubmoduleDisplayName = $"{DisplayName}.Examination.Settings.ExamTypes";
			
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
