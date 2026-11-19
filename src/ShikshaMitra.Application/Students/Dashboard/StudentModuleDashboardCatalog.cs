using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ShikshaMitra.Constants.Permissions;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

public static class StudentModuleDashboardCatalog
{
	public static string ResolveMode(IEnumerable<string> roles)
	{
		var names = roles.Select(role => Regex.Replace(Regex.Replace(role.Trim(), "([a-z])([A-Z])", "$1 $2")
			.Replace('_', ' ').Replace('-', ' '), @"\s+", " ").ToLowerInvariant()).ToHashSet();
		if (names.Overlaps(["admin", "administrator", "super admin"])) return "admin";
		if (names.Overlaps(["principal", "vice principal", "hod", "head of department", "academic coordinator"])) return "leadership";
		if (names.Overlaps(["teacher", "class teacher"])) return "teacher";
		return "common";
	}

	public static StudentModuleDashboardLinkDto[] Links =>
	[
		Link("inquiries", "Admissions inquiries", "Follow up with prospective families.", "/student/admissions/inquiries", "mdi:account-question-outline", P.AdmissionInquiries.View, ShikshaMitraSubModulePermissions.Enrollment),
		Link("applications", "Applications", "Continue applications and admission decisions.", "/student/admissions/applications", "mdi:application-edit-outline", P.AdmissionApplications.View, ShikshaMitraSubModulePermissions.Enrollment),
		Link("assessments", "Admission assessments", "Manage scheduled assessments and results.", "/student/admissions/assessments", "mdi:clipboard-clock-outline", P.AdmissionAssessments.View, ShikshaMitraSubModulePermissions.Enrollment),
		Link("reviews", "Application reviews", "Review applications assigned for a decision.", "/student/admissions/reviews", "mdi:clipboard-check-outline", P.AdmissionReviews.View, ShikshaMitraSubModulePermissions.Enrollment),
		Link("onboarding", "Student setup", "Complete readiness, placement and account handoffs.", "/student/onboarding/setup", "mdi:account-check-outline", P.StudentOnboarding.View, ShikshaMitraSubModulePermissions.StudentOnboarding),
		Link("directory", "Student directory", "Find active and former permanent records.", "/student/records/directory", "mdi:account-school-outline", P.Records_StudentList.View, ShikshaMitraSubModulePermissions.Records),
		Link("changes", "Record changes", "Follow requests and approvals.", "/student/records/changes", "mdi:account-edit-outline", P.Records_Changes.View, ShikshaMitraSubModulePermissions.Records),
		Link("transitions", "Student transitions", "Manage promotions, exits and annual renewals.", "/student/records/transitions", "mdi:transfer-outline", P.Records_Transitions.View, ShikshaMitraSubModulePermissions.Records),
		Link("documents", "Student documents", "Issue and review certificates and identity cards.", "/student/records/documents", "mdi:file-document-outline", P.Records_Certificates.View, ShikshaMitraSubModulePermissions.Records),
		Link("reports", "Student reports", "Generate scoped student reports.", "/student/records/reports", "mdi:chart-box-outline", P.Records_Reports.View, ShikshaMitraSubModulePermissions.Records),
		Link("attendance", "Attendance register", "Save and submit daily attendance.", "/student/attendance/take-attendance", "mdi:calendar-check-outline", P.Attendance_MarkAttendance.View, ShikshaMitraSubModulePermissions.Attendance),
		Link("leave", "Leave and corrections", "Review leave requests and attendance corrections.", "/student/attendance/leave-absent", "mdi:calendar-edit-outline", P.Attendance_LeaveAndAbsent.View, ShikshaMitraSubModulePermissions.Attendance),
		Link("attendance-reports", "Attendance reports", "Review policy-based percentages and completeness.", "/student/attendance/reports", "mdi:chart-line", P.Attendance_Reports.View, ShikshaMitraSubModulePermissions.Attendance)
	];

	private static StudentModuleDashboardLinkDto Link(string id, string label, string description, string href,
		string icon, string permission, string visibility) => new()
		{ Id = id, Label = label, Description = description, Href = href, Icon = icon, Permission = permission, VisibilityPermission = visibility };
}
