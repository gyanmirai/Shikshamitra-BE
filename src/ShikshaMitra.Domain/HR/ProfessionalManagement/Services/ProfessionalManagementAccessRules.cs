using System;

namespace ShikshaMitra.HR.ProfessionalManagement;

public static class ProfessionalManagementAccessRules
{
	public static bool CanSelfReview(Guid? currentEmployeeId, PerformanceReviewAssignment assignment) =>
		currentEmployeeId == assignment.EmployeeId;

	public static bool CanReviewEmployee(Guid? currentEmployeeId, PerformanceReviewAssignment assignment) =>
		currentEmployeeId.HasValue &&
		(currentEmployeeId == assignment.ReportingManagerEmployeeId ||
		 currentEmployeeId == assignment.HodEmployeeId ||
		 currentEmployeeId == assignment.PrincipalEmployeeId);

	public static bool CanUploadEvidence(
		Guid? currentEmployeeId,
		PerformanceReviewAssignment assignment,
		bool hasSelfReviewPermission,
		bool hasReviewEmployeesPermission) =>
		(hasSelfReviewPermission && CanSelfReview(currentEmployeeId, assignment)) ||
		(hasReviewEmployeesPermission && CanReviewEmployee(currentEmployeeId, assignment));

	public static bool CanAddObservation(Guid? currentEmployeeId, PerformanceReviewAssignment assignment, Guid observerEmployeeId) =>
		currentEmployeeId == observerEmployeeId && CanReviewEmployee(currentEmployeeId, assignment);
}
