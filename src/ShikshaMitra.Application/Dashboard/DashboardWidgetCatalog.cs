using System;
using System.Collections.Generic;
using ShikshaMitra.Constants.Permissions;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

internal static class DashboardWidgetCatalog
{
	public static readonly string[] StudentAttendancePermissions =
	[
		ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View
	];

	public static readonly IReadOnlyDictionary<string, DashboardWidgetDefinition> WidgetDefinitions =
		new Dictionary<string, DashboardWidgetDefinition>(StringComparer.Ordinal)
		{
			["announcement-board-practical"] = new(
				Announcements,
				"/academic/scheduleExam",
				[ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View]),
			["announcement-ptm"] = new(
				Announcements,
				"/communication/dashboard",
				[ShikshaMitraSubModulePermissions.CommunicationDashboard]),
			["calendar-periodic-test"] = new(
				Calendar,
				"/academic/scheduleExam",
				[ShikshaMitraSubmodulePermissions.Academics.Examination_ScheduleExam.View]),
			["calendar-safety-drill"] = new(
				Calendar,
				"/calendar/view",
				[ShikshaMitraSubModulePermissions.CalendarView]),
			["leadership-total-students"] = new(
				Kpis,
				"/student/profile",
				[ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View]),
			["leadership-fee-collection"] = new(
				Kpis,
				"/finance/fee-collection",
				[ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View]),
			["leadership-attendance"] = new(
				Kpis,
				"/student/attendance/take-attendance",
				StudentAttendancePermissions),
			["leadership-compliance"] = new(
				Kpis,
				"/setting/basic-information",
				[ShikshaMitraModulePermissions.InstituteSetup]),
			["leadership-admission-approvals"] = new(
				Actions,
				"/student/enrollment/admission",
				[ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View],
				"Admissions"),
			["leadership-payroll-review"] = new(
				Actions,
				"/hr/payroll/run-payroll",
				[ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View],
				"HR"),
			["leadership-certificate-risk"] = new(
				Risks,
				"/setting/basic-information",
				[ShikshaMitraModulePermissions.InstituteSetup],
				"Admin office"),
			["leadership-academic-health"] = new(
				Modules,
				"/academic/dashboard",
				[ShikshaMitraSubmodulePermissions.Academics.Dashboard.View]),
			["leadership-transport-health"] = new(
				Modules,
				"/transport/dashboard",
				[ShikshaMitraModulePermissions.Transport]),
			["principal-transport-applications"] = new(
				Actions,
				"/transport/application-management",
				[ShikshaMitraSubModulePermissions.ApplicationManagement],
				"Transport"),
			["principal-student-attendance"] = new(
				Kpis,
				"/student/attendance/take-attendance",
				StudentAttendancePermissions),
			["principal-teacher-attendance"] = new(
				Kpis,
				"/hr/attendance-tracking",
				[ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View]),
			["principal-curriculum"] = new(
				Kpis,
				"/academic/courseSubjects",
				[ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_ChapterManagement.View]),
			["principal-pending-admissions"] = new(
				Kpis,
				"/student/enrollment/admission",
				[ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View]),
			["principal-leave-approvals"] = new(
				Actions,
				"/hr/leave-approvals",
				[ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View],
				"HR"),
			["principal-weak-classes"] = new(
				Risks,
				"/academic/dashboard",
				[ShikshaMitraSubmodulePermissions.Academics.Dashboard.View],
				"Academic coordinator"),
			["principal-academic-dashboard"] = new(
				Modules,
				"/academic/dashboard",
				[ShikshaMitraSubmodulePermissions.Academics.Dashboard.View]),
			["principal-security-dashboard"] = new(
				Modules,
				"/security-guard/dashboard",
				[ShikshaMitraSubModulePermissions.SecurityGuardDashboard]),
			["teacher-classes"] = new(
				Kpis,
				"/academic/newTimetable",
				[ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View]),
			["teacher-homework"] = new(
				Kpis,
				"/academic/gradingAndFeedback",
				[ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View]),
			["teacher-exam-evaluation"] = new(
				Kpis,
				"/academic/gradeEntry",
				[ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View]),
			["teacher-attendance"] = new(
				Actions,
				"/student/attendance/take-attendance",
				StudentAttendancePermissions,
				"Teacher"),
			["teacher-student-risk"] = new(
				Risks,
				"/student/profile",
				[ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View],
				"Class teacher"),
			["teacher-class-performance"] = new(
				Modules,
				"/academic/dashboard",
				[ShikshaMitraSubmodulePermissions.Academics.Dashboard.View]),
			["finance-collection"] = new(
				Kpis,
				"/finance/fee-collection",
				[ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View]),
			["finance-dues"] = new(
				Kpis,
				"/finance/fee-collection",
				[ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View]),
			["hr-payroll-readiness"] = new(
				Kpis,
				"/hr/payroll/dashboard",
				[ShikshaMitraSubmodulePermissions.HR.PayrollManagement_Dashboard.View]),
			["hr-staff-attendance"] = new(
				Kpis,
				"/hr/attendance-tracking",
				[ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View]),
			["finance-reconciliation"] = new(
				Actions,
				"/finance/reconciliation",
				[ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.View],
				"Finance"),
			["hr-leave-approvals"] = new(
				Actions,
				"/hr/leave-approvals",
				[ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View],
				"HR"),
			["finance-concession-impact"] = new(
				Risks,
				"/finance/concession",
				[ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View],
				"Finance officer"),
			["finance-dashboard"] = new(
				Modules,
				"/finance/dashboard",
				[ShikshaMitraModulePermissions.Finance]),
			["hr-dashboard"] = new(
				Modules,
				"/hr/dashboard",
				[ShikshaMitraSubModulePermissions.HRDashboard]),
			["student-attendance"] = new(
				Kpis,
				"/student/dashboard",
				[ShikshaMitraModulePermissions.Students]),
			["student-assignments"] = new(
				Kpis,
				"/academic/homeworkCreation",
				[ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View]),
			["student-fee-status"] = new(
				Kpis,
				"/finance/fee-collection",
				[ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View]),
			["student-document-update"] = new(
				Actions,
				"/student/profile",
				[ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View],
				"Parent"),
			["student-transport-alert"] = new(
				Risks,
				"/transport/dashboard",
				[ShikshaMitraSubModulePermissions.TransportDashboard],
				"Transport"),
			["student-academic-summary"] = new(
				Modules,
				"/student/dashboard",
				[ShikshaMitraModulePermissions.Students])
		};
}

internal sealed record DashboardWidgetDefinition(
	string Zone,
	string DrilldownUrl,
	string[] RequiredPermissions,
	string? Owner = null);
