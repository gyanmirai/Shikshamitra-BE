using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
using System;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private static PerformanceStaffCategory InferPerformanceSeedStaffCategory(Employee employee)
	{
		var text = $"{employee.Designation} {employee.Department} {employee.Role?.Name}".ToLowerInvariant();
		if (text.Contains("class teacher")) return PerformanceStaffCategory.ClassTeacher;
		if (text.Contains("account")) return PerformanceStaffCategory.Accounts;
		if (text.Contains("librarian") || text.Contains("lab")) return PerformanceStaffCategory.LibrarianLabAssistant;
		if (text.Contains("teacher") || text.Contains("faculty") || text.Contains("academic")) return PerformanceStaffCategory.Teacher;
		return PerformanceStaffCategory.Admin;
	}

	private static string BuildPerformanceStageComment(Employee employee, PerformanceReviewStageType stageType) =>
		stageType switch
		{
			PerformanceReviewStageType.SelfAppraisal => $"{employee.EmployeeName} documented core achievements and improvement priorities.",
			PerformanceReviewStageType.ReportingManager => "Reporting manager noted consistent follow-through and practical classroom ownership.",
			PerformanceReviewStageType.HodCoordinator => "Department review confirms alignment with academic and operational expectations.",
			PerformanceReviewStageType.PrincipalManagement => "Leadership review recommends continued development with measured recognition.",
			PerformanceReviewStageType.HrVerification => "HR verified documentation completeness and workflow readiness.",
			_ => "Review stage seeded for demo workflow."
		};
}
