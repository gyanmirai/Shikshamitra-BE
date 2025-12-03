using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Academics module permissions and submodules.
/// This file is split into multiple partial files for better maintainability:
/// - ShikshaMitraSubmodulePermissions.Academics.cs (Main module definition)
/// - ShikshaMitraSubmodulePermissions.Academics.Curriculum.cs
/// - ShikshaMitraSubmodulePermissions.Academics.Timetable.cs
/// - ShikshaMitraSubmodulePermissions.Academics.HomeworkAssignment.cs
/// - ShikshaMitraSubmodulePermissions.Academics.Examination.cs
/// - ShikshaMitraSubmodulePermissions.Academics.Group.cs
/// - ShikshaMitraSubmodulePermissions.Academics.LMS.cs
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Academics module permissions
	/// </summary>
	public static partial class Academics
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

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		/// <summary>
		/// Module-level display names
		/// </summary>
		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		/// <summary>
		/// Academics Dashboard permissions
		/// </summary>
		public static class Dashboard
		{
			private const string SubmoduleName = $"{ModuleName}.Dashboard";
			private const string SubmoduleDisplayName = $"{DisplayName}.Dashboard";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { Default, View };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" }
			};
		}

		/// <summary>
		/// Academics Notification permissions
		/// </summary>
		public static class Notification
		{
			private const string SubmoduleName = $"{ModuleName}.Notification";
			private const string SubmoduleDisplayName = $"{DisplayName}.Notification";

			public const string Default = SubmoduleName;
			public const string View = $"{SubmoduleName}.View";
			public const string Add = $"{SubmoduleName}.Add";
			public const string Update = $"{SubmoduleName}.Update";

			public static readonly string[] All = { Default, View, Add, Update };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Default, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Add, $"{SubmoduleDisplayName}.Add" },
				{ Update, $"{SubmoduleDisplayName}.Update" }
			};
		}

		/// <summary>
		/// Helper to get all submodules.
		/// This registry is populated across multiple partial files.
		/// </summary>
		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			// Dashboard
			{ Dashboard.Default, new SubmoduleInfo(Dashboard.All, Dashboard.DisplayNames) },

			// Curriculum Management submodules (defined in Academics.Curriculum.cs)
			{ CurriculumManagement_SubjectAssignment.Default, new SubmoduleInfo(CurriculumManagement_SubjectAssignment.All, CurriculumManagement_SubjectAssignment.DisplayNames) },
			{ CurriculumManagement_ChapterManagement.Default, new SubmoduleInfo(CurriculumManagement_ChapterManagement.All, CurriculumManagement_ChapterManagement.DisplayNames) },
			{ CurriculumManagement_Resources.Default, new SubmoduleInfo(CurriculumManagement_Resources.All, CurriculumManagement_Resources.DisplayNames) },
			{ CurriculumManagement_Reports.Default, new SubmoduleInfo(CurriculumManagement_Reports.All, CurriculumManagement_Reports.DisplayNames) },
			{ CurriculumManagement_SettingsSubjectCreation.Default, new SubmoduleInfo(CurriculumManagement_SettingsSubjectCreation.All, CurriculumManagement_SettingsSubjectCreation.DisplayNames) },
			{ CurriculumManagement_SettingsElectiveGroupCreation.Default, new SubmoduleInfo(CurriculumManagement_SettingsElectiveGroupCreation.All, CurriculumManagement_SettingsElectiveGroupCreation.DisplayNames) },

			// Timetable submodules (defined in Academics.Timetable.cs)
			{ Timetable_TimetableManagement.Default, new SubmoduleInfo(Timetable_TimetableManagement.All, Timetable_TimetableManagement.DisplayNames) },
			{ Timetable_Reports.Default, new SubmoduleInfo(Timetable_Reports.All, Timetable_Reports.DisplayNames) },
			{ Timetable_SettingsWeeklyLectures.Default, new SubmoduleInfo(Timetable_SettingsWeeklyLectures.All, Timetable_SettingsWeeklyLectures.DisplayNames) },
			{ Timetable_SettingsLectureSlots.Default, new SubmoduleInfo(Timetable_SettingsLectureSlots.All, Timetable_SettingsLectureSlots.DisplayNames) },

			// Homework And Assignment submodules (defined in Academics.HomeworkAssignment.cs)
			{ HomeworkAndAssignment_LearningTasks.Default, new SubmoduleInfo(HomeworkAndAssignment_LearningTasks.All, HomeworkAndAssignment_LearningTasks.DisplayNames) },
			{ HomeworkAndAssignment_AssessmentAndFeedback.Default, new SubmoduleInfo(HomeworkAndAssignment_AssessmentAndFeedback.All, HomeworkAndAssignment_AssessmentAndFeedback.DisplayNames) },
			{ HomeworkAndAssignment_Reports.Default, new SubmoduleInfo(HomeworkAndAssignment_Reports.All, HomeworkAndAssignment_Reports.DisplayNames) },
			{ HomeworkAndAssignment_SettingsHomeworkTypes.Default, new SubmoduleInfo(HomeworkAndAssignment_SettingsHomeworkTypes.All, HomeworkAndAssignment_SettingsHomeworkTypes.DisplayNames) },
			{ HomeworkAndAssignment_SettingsAssignmentTypes.Default, new SubmoduleInfo(HomeworkAndAssignment_SettingsAssignmentTypes.All, HomeworkAndAssignment_SettingsAssignmentTypes.DisplayNames) },

			// Examination submodules (defined in Academics.Examination.cs)
			{ Examination_ScheduleExam.Default, new SubmoduleInfo(Examination_ScheduleExam.All, Examination_ScheduleExam.DisplayNames) },
			{ Examination_SeatAllocation.Default, new SubmoduleInfo(Examination_SeatAllocation.All, Examination_SeatAllocation.DisplayNames) },
			{ Examination_RecordMarks.Default, new SubmoduleInfo(Examination_RecordMarks.All, Examination_RecordMarks.DisplayNames) },
			{ Examination_QuizManagement.Default, new SubmoduleInfo(Examination_QuizManagement.All, Examination_QuizManagement.DisplayNames) },
			{ Examination_Reports.Default, new SubmoduleInfo(Examination_Reports.All, Examination_Reports.DisplayNames) },
			{ Examination_SettingsGradeScaling.Default, new SubmoduleInfo(Examination_SettingsGradeScaling.All, Examination_SettingsGradeScaling.DisplayNames) },
			{ Examination_SettingsExamTypes.Default, new SubmoduleInfo(Examination_SettingsExamTypes.All, Examination_SettingsExamTypes.DisplayNames) },

			// Group Management submodules (defined in Academics.Group.cs)
			{ GroupManagement_Dashboard.Default, new SubmoduleInfo(GroupManagement_Dashboard.All, GroupManagement_Dashboard.DisplayNames) },
			{ GroupManagement_DynamicGroup.Default, new SubmoduleInfo(GroupManagement_DynamicGroup.All, GroupManagement_DynamicGroup.DisplayNames) },
			{ GroupManagement_ActivityManagement.Default, new SubmoduleInfo(GroupManagement_ActivityManagement.All, GroupManagement_ActivityManagement.DisplayNames) },

			// Learning Management System submodules (defined in Academics.LMS.cs)
			{ LearningManagementSystem_Dashboard.Default, new SubmoduleInfo(LearningManagementSystem_Dashboard.All, LearningManagementSystem_Dashboard.DisplayNames) },
			{ LearningManagementSystem_Course.Default, new SubmoduleInfo(LearningManagementSystem_Course.All, LearningManagementSystem_Course.DisplayNames) },
			{ LearningManagementSystem_StudentProgressTracking.Default, new SubmoduleInfo(LearningManagementSystem_StudentProgressTracking.All, LearningManagementSystem_StudentProgressTracking.DisplayNames) },
			{ LearningManagementSystem_ContentLibrary.Default, new SubmoduleInfo(LearningManagementSystem_ContentLibrary.All, LearningManagementSystem_ContentLibrary.DisplayNames) },
			{ LearningManagementSystem_ReportsAndAnalytics.Default, new SubmoduleInfo(LearningManagementSystem_ReportsAndAnalytics.All, LearningManagementSystem_ReportsAndAnalytics.DisplayNames) },
			{ LearningManagementSystem_SettingsGeneral.Default, new SubmoduleInfo(LearningManagementSystem_SettingsGeneral.All, LearningManagementSystem_SettingsGeneral.DisplayNames) },
			{ LearningManagementSystem_SettingsCourseManagement.Default, new SubmoduleInfo(LearningManagementSystem_SettingsCourseManagement.All, LearningManagementSystem_SettingsCourseManagement.DisplayNames) },
			{ LearningManagementSystem_SettingsContentManagement.Default, new SubmoduleInfo(LearningManagementSystem_SettingsContentManagement.All, LearningManagementSystem_SettingsContentManagement.DisplayNames) },
			{ LearningManagementSystem_SettingsNotification.Default, new SubmoduleInfo(LearningManagementSystem_SettingsNotification.All, LearningManagementSystem_SettingsNotification.DisplayNames) },
			{ LearningManagementSystem_SettingsIntegration.Default, new SubmoduleInfo(LearningManagementSystem_SettingsIntegration.All, LearningManagementSystem_SettingsIntegration.DisplayNames) },

			// Notification
			{ Notification.Default, new SubmoduleInfo(Notification.All, Notification.DisplayNames) }
		};
	}
}
