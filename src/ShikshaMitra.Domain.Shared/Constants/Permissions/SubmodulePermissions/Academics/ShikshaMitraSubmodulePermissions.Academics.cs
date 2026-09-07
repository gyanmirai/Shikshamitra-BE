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
/// - ShikshaMitraSubmodulePermissions.Academics.QuestionBank.cs
/// - ShikshaMitraSubmodulePermissions.Academics.QuestionPapers.cs
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

			public const string View = $"{SubmoduleName}.View";

			public static readonly string[] All = { View };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
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

		/// <summary>
		/// Academics audit trail permissions
		/// </summary>
		public static class Audits
		{
			private const string SubmoduleName = $"{ModuleName}.Audits";
			private const string SubmoduleDisplayName = $"{DisplayName}.Audits";

			public const string View = $"{SubmoduleName}.View";
			public const string Export = $"{SubmoduleName}.Export";
			public const string ViewSensitiveMetadata = $"{SubmoduleName}.ViewSensitiveMetadata";

			public static readonly string[] All = { View, Export, ViewSensitiveMetadata };

			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ SubmoduleName, SubmoduleDisplayName },
				{ View, $"{SubmoduleDisplayName}.View" },
				{ Export, $"{SubmoduleDisplayName}.Export" },
				{ ViewSensitiveMetadata, $"{SubmoduleDisplayName}.ViewSensitiveMetadata" }
			};
		}

		/// <summary>
		/// Helper to get all submodules.
		/// This registry is populated across multiple partial files.
		/// </summary>
		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			// Dashboard
			{ "Academics.Dashboard", new SubmoduleInfo(Dashboard.All, Dashboard.DisplayNames) },

			// Curriculum Management submodules (defined in Academics.Curriculum.cs)
			{ "Academics.CurriculumManagement.SubjectAssignment", new SubmoduleInfo(CurriculumManagement_SubjectAssignment.All, CurriculumManagement_SubjectAssignment.DisplayNames) },
			{ "Academics.CurriculumManagement.ElectiveAssignment", new SubmoduleInfo(CurriculumManagement_ElectiveAssignment.All, CurriculumManagement_ElectiveAssignment.DisplayNames) },
			{ "Academics.CurriculumManagement.ChapterManagement", new SubmoduleInfo(CurriculumManagement_ChapterManagement.All, CurriculumManagement_ChapterManagement.DisplayNames) },
			{ "Academics.CurriculumManagement.LessonPlan", new SubmoduleInfo(CurriculumManagement_LessonPlan.All, CurriculumManagement_LessonPlan.DisplayNames) },
			{ "Academics.CurriculumManagement.Resources", new SubmoduleInfo(CurriculumManagement_Resources.All, CurriculumManagement_Resources.DisplayNames) },
			{ "Academics.CurriculumManagement.Reports", new SubmoduleInfo(CurriculumManagement_Reports.All, CurriculumManagement_Reports.DisplayNames) },
			{ "Academics.CurriculumManagement.Settings.SubjectCreation", new SubmoduleInfo(CurriculumManagement_SettingsSubjectCreation.All, CurriculumManagement_SettingsSubjectCreation.DisplayNames) },
			{ "Academics.CurriculumManagement.Settings.ElectiveGroupCreation", new SubmoduleInfo(CurriculumManagement_SettingsElectiveGroupCreation.All, CurriculumManagement_SettingsElectiveGroupCreation.DisplayNames) },

			// Timetable submodules (defined in Academics.Timetable.cs)
			{ "Academics.Timetable.TimetableManagement", new SubmoduleInfo(Timetable_TimetableManagement.All, Timetable_TimetableManagement.DisplayNames) },
			{ "Academics.Timetable.Reports", new SubmoduleInfo(Timetable_Reports.All, Timetable_Reports.DisplayNames) },
			{ "Academics.Timetable.Settings.WeeklyLectures", new SubmoduleInfo(Timetable_SettingsWeeklyLectures.All, Timetable_SettingsWeeklyLectures.DisplayNames) },
			{ "Academics.Timetable.Settings.LectureSlots", new SubmoduleInfo(Timetable_SettingsLectureSlots.All, Timetable_SettingsLectureSlots.DisplayNames) },

			// Homework And Assignment submodules (defined in Academics.HomeworkAssignment.cs)
			{ "Academics.HomeworkAndAssignment.LearningTasks", new SubmoduleInfo(HomeworkAndAssignment_LearningTasks.All, HomeworkAndAssignment_LearningTasks.DisplayNames) },
			{ "Academics.HomeworkAndAssignment.AssessmentAndFeedback", new SubmoduleInfo(HomeworkAndAssignment_AssessmentAndFeedback.All, HomeworkAndAssignment_AssessmentAndFeedback.DisplayNames) },
			{ "Academics.HomeworkAndAssignment.Reports", new SubmoduleInfo(HomeworkAndAssignment_Reports.All, HomeworkAndAssignment_Reports.DisplayNames) },
			{ "Academics.HomeworkAndAssignment.Settings.HomeworkTypes", new SubmoduleInfo(HomeworkAndAssignment_SettingsHomeworkTypes.All, HomeworkAndAssignment_SettingsHomeworkTypes.DisplayNames) },
			{ "Academics.HomeworkAndAssignment.Settings.AssignmentTypes", new SubmoduleInfo(HomeworkAndAssignment_SettingsAssignmentTypes.All, HomeworkAndAssignment_SettingsAssignmentTypes.DisplayNames) },
			{ "Academics.HomeworkAndAssignment.Settings.RubricConfig", new SubmoduleInfo(HomeworkAndAssignment_SettingsRubricConfig.All, HomeworkAndAssignment_SettingsRubricConfig.DisplayNames) },

			// Examination submodules (defined in Academics.Examination.cs)
			{ "Academics.Examination.ScheduleExam", new SubmoduleInfo(Examination_ScheduleExam.All, Examination_ScheduleExam.DisplayNames) },
			{ "Academics.Examination.SeatAllocation", new SubmoduleInfo(Examination_SeatAllocation.All, Examination_SeatAllocation.DisplayNames) },
			{ "Academics.Examination.RecordMarks", new SubmoduleInfo(Examination_RecordMarks.All, Examination_RecordMarks.DisplayNames) },
			{ "Academics.Examination.QuizManagement", new SubmoduleInfo(Examination_QuizManagement.All, Examination_QuizManagement.DisplayNames) },
			{ "Academics.Examination.Reports", new SubmoduleInfo(Examination_Reports.All, Examination_Reports.DisplayNames) },
			{ "Academics.Examination.ResultCreation", new SubmoduleInfo(Examination_ResultCreation.All, Examination_ResultCreation.DisplayNames) },
			{ "Academics.Examination.Result", new SubmoduleInfo(Examination_Result.All, Examination_Result.DisplayNames) },
			{ "Academics.Examination.Settings.GradeScaling", new SubmoduleInfo(Examination_SettingsGradeScaling.All, Examination_SettingsGradeScaling.DisplayNames) },
				{ "Academics.Examination.Settings.ExamTypes", new SubmoduleInfo(Examination_SettingsExamTypes.All, Examination_SettingsExamTypes.DisplayNames) },
				{ "Academics.Examination.QuestionBank", new SubmoduleInfo(Examination_QuestionBank.All, Examination_QuestionBank.DisplayNames) },
				{ "Academics.Examination.QuestionPapers", new SubmoduleInfo(Examination_QuestionPapers.All, Examination_QuestionPapers.DisplayNames) },

			// Group Management submodules (defined in Academics.Group.cs)
			{ "Academics.GroupManagement.Dashboard", new SubmoduleInfo(GroupManagement_Dashboard.All, GroupManagement_Dashboard.DisplayNames) },
			{ "Academics.GroupManagement.DynamicGroup", new SubmoduleInfo(GroupManagement_DynamicGroup.All, GroupManagement_DynamicGroup.DisplayNames) },
			{ "Academics.GroupManagement.ActivityManagement", new SubmoduleInfo(GroupManagement_ActivityManagement.All, GroupManagement_ActivityManagement.DisplayNames) },

			// Learning Management System submodules (defined in Academics.LMS.cs)
			{ "Academics.LearningManagementSystem.Dashboard", new SubmoduleInfo(LearningManagementSystem_Dashboard.All, LearningManagementSystem_Dashboard.DisplayNames) },
			{ "Academics.LearningManagementSystem.Course", new SubmoduleInfo(LearningManagementSystem_Course.All, LearningManagementSystem_Course.DisplayNames) },
			{ "Academics.LearningManagementSystem.StudentProgressTracking", new SubmoduleInfo(LearningManagementSystem_StudentProgressTracking.All, LearningManagementSystem_StudentProgressTracking.DisplayNames) },
			{ "Academics.LearningManagementSystem.ContentLibrary", new SubmoduleInfo(LearningManagementSystem_ContentLibrary.All, LearningManagementSystem_ContentLibrary.DisplayNames) },
			{ "Academics.LearningManagementSystem.ReportsAndAnalytics", new SubmoduleInfo(LearningManagementSystem_ReportsAndAnalytics.All, LearningManagementSystem_ReportsAndAnalytics.DisplayNames) },
			{ "Academics.LearningManagementSystem.Settings.General", new SubmoduleInfo(LearningManagementSystem_SettingsGeneral.All, LearningManagementSystem_SettingsGeneral.DisplayNames) },
			{ "Academics.LearningManagementSystem.Settings.CourseManagement", new SubmoduleInfo(LearningManagementSystem_SettingsCourseManagement.All, LearningManagementSystem_SettingsCourseManagement.DisplayNames) },
			{ "Academics.LearningManagementSystem.Settings.ContentManagement", new SubmoduleInfo(LearningManagementSystem_SettingsContentManagement.All, LearningManagementSystem_SettingsContentManagement.DisplayNames) },
			{ "Academics.LearningManagementSystem.Settings.Notification", new SubmoduleInfo(LearningManagementSystem_SettingsNotification.All, LearningManagementSystem_SettingsNotification.DisplayNames) },
			{ "Academics.LearningManagementSystem.Settings.Integration", new SubmoduleInfo(LearningManagementSystem_SettingsIntegration.All, LearningManagementSystem_SettingsIntegration.DisplayNames) },

			// Notification
			{ "Academics.Notification", new SubmoduleInfo(Notification.All, Notification.DisplayNames) },

			// Academic audits
			{ "Academics.Audits", new SubmoduleInfo(Audits.All, Audits.DisplayNames) },

			// Academic Calendar
			{ "Academics.AcademicCalendar", new SubmoduleInfo(Academics_AcademicCalendar.All, Academics_AcademicCalendar.DisplayNames) },
			{ "Academics.AcademicCalendar.Dashboard", new SubmoduleInfo(AcademicCalendar_Dashboard.All, AcademicCalendar_Dashboard.DisplayNames) },
			{ "Academics.AcademicCalendar.CalendarManagement", new SubmoduleInfo(AcademicCalendar_CalendarManagement.All, AcademicCalendar_CalendarManagement.DisplayNames) },
			{ "Academics.AcademicCalendar.EmergencyClosure", new SubmoduleInfo(AcademicCalendar_EmergencyClosure.All, AcademicCalendar_EmergencyClosure.DisplayNames) },
			{ "Academics.AcademicCalendar.ImpactLogs", new SubmoduleInfo(AcademicCalendar_ImpactLogs.All, AcademicCalendar_ImpactLogs.DisplayNames) },
			{ "Academics.AcademicCalendar.AuditLogs", new SubmoduleInfo(AcademicCalendar_AuditLogs.All, AcademicCalendar_AuditLogs.DisplayNames) },
			{ "Academics.AcademicCalendar.Settings", new SubmoduleInfo(AcademicCalendar_Settings.All, AcademicCalendar_Settings.DisplayNames) }
		};
	}
}
