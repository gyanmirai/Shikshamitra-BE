using ShikshaMitra.Constants.Permissions;
using System.Collections.Generic;

namespace ShikshaMitra.Reporting.Agent;

public static class ReportAgentModuleCatalog
{
	public static IReadOnlyList<ReportAgentModuleDefinition> All { get; } =
	[
		new("academic.curriculum", "Curriculum", "Subjects, chapters, curriculum coverage and teacher assignments.",
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.View,
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.Add,
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_Reports.Add,
			ReportAgentModuleAvailability.Available),
		new("academic.timetable", "Timetable", "Class, teacher, room and timetable distribution reports.",
			ShikshaMitraSubmodulePermissions.Academics.Timetable_Reports.View,
			ShikshaMitraSubmodulePermissions.Academics.Timetable_Reports.Add,
			ShikshaMitraSubmodulePermissions.Academics.Timetable_Reports.Add,
			ReportAgentModuleAvailability.Available),
		new("academic.homework-assignment", "Homework & Assignment", "Issuance, submissions, evaluation and workload reports.",
			ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_Reports.View,
			ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_Reports.Add,
			ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_Reports.Add,
			ReportAgentModuleAvailability.Available),
		new("academic.examination", "Examination", "Marks, results, grades and exam analysis reports.",
			ShikshaMitraSubmodulePermissions.Academics.Examination_Reports.View,
			ShikshaMitraSubmodulePermissions.Academics.Examination_Reports.Add,
			ShikshaMitraSubmodulePermissions.Academics.Examination_Reports.Add,
			ReportAgentModuleAvailability.Available),
		new("academic.lms", "Learning Management", "Learning activity and analytics reports.",
			ShikshaMitraSubmodulePermissions.Academics.LearningManagementSystem_ReportsAndAnalytics.View,
			ShikshaMitraSubmodulePermissions.Academics.LearningManagementSystem_ReportsAndAnalytics.Add,
			ShikshaMitraSubmodulePermissions.Academics.LearningManagementSystem_ReportsAndAnalytics.Add),
		new("students.records", "Student Records", "Student profile, enrollment and record reports.",
			ShikshaMitraSubmodulePermissions.Students.Records_Reports.View,
			ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add,
			ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add,
			ReportAgentModuleAvailability.Available),
		new("students.attendance", "Student Attendance", "Attendance, absence and attendance trend reports.",
			ShikshaMitraSubmodulePermissions.Students.Attendance_Reports.View,
			ShikshaMitraSubmodulePermissions.Students.Attendance_Reports.Add,
			ShikshaMitraSubmodulePermissions.Students.Attendance_Reports.Add,
			ReportAgentModuleAvailability.Available),
		new("hr.reports", "HR", "Employee, attendance, leave, payroll and performance reports.",
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Build,
			ShikshaMitraSubmodulePermissions.HR.Reports.Export,
			ReportAgentModuleAvailability.Available),
		new("finance.reports", "Finance", "Fee, collection, accounting and finance reports.",
			ShikshaMitraSubModulePermissions.FinanceReports,
			ShikshaMitraSubModulePermissions.FinanceReports,
			ShikshaMitraSubModulePermissions.FinanceReports,
			ReportAgentModuleAvailability.Available),
		new("library.reports", "Library", "Circulation, inventory, fines and library activity reports.",
			ShikshaMitraSubmodulePermissions.Library.Reports.View,
			ShikshaMitraSubmodulePermissions.Library.Reports.Export,
			ShikshaMitraSubmodulePermissions.Library.Reports.Export,
			ReportAgentModuleAvailability.Available),
		new("transport.reports", "Transport", "Vehicle, route, passenger, compliance and transport fee reports.",
			ShikshaMitraSubModulePermissions.TransportReports,
			ShikshaMitraSubModulePermissions.TransportReports,
			ShikshaMitraSubModulePermissions.TransportReports,
			ReportAgentModuleAvailability.Available)
	];
}

public sealed record ReportAgentModuleDefinition(
	string Id,
	string Label,
	string Description,
	string ViewPermission,
	string BuildPermission,
	string ExportPermission,
	string Availability = ReportAgentModuleAvailability.CatalogOnly);
