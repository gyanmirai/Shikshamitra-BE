using System;
using System.Linq;
using System.Linq.Expressions;

namespace ShikshaMitra.HR.Employees.Onboarding;

public static class EmployeeApplicationListProjection
{
	// Keep client-side display formatting at the top-level projection. Passing an entity
	// to a mapper here would materialize its owned JSON snapshots and tracking graph.
	public static Expression<Func<EmployeeApplication, EmployeeApplicationListItemDto>> Item => application => new EmployeeApplicationListItemDto
	{
		Id = application.Id,
		ApplicationNumber = application.ApplicationNumber,
		Status = application.Status,
		FullName = JoinName(application.FirstName, application.MiddleName, application.LastName),
		Email = application.Email,
		MobileNo = application.MobileNo,
		PositionAppliedFor = application.PositionAppliedFor,
		DepartmentAppliedFor = application.DepartmentAppliedFor,
		WorkAssignment = Assignment(application.WorkAssignmentMode, application.WorkAssignmentAcademicYearId,
			application.WorkAssignmentAcademicYearName, application.WorkAssignmentOrganizationIds, application.WorkAssignmentOrganizationLabels),
		WorkAssignmentDisplay = AssignmentDisplay(application.WorkAssignmentMode, application.WorkAssignmentAcademicYearName,
			application.WorkAssignmentOrganizationLabels, application.DepartmentAppliedFor),
		TokenExpiresAt = application.TokenExpiresAt,
		LinkLastIssuedAt = application.LinkLastIssuedAt,
		SubmittedAt = application.SubmittedAt,
		FinalizedAt = application.FinalizedAt,
		FinalizedEmployeeId = application.FinalizedEmployeeId,
		HasResume = HasUpload(application.ResumeBlobName),
		HasPhoto = HasUpload(application.PhotoBlobName),
		CreationTime = application.CreationTime
	};

	private static bool HasUpload(string? blobName) => !string.IsNullOrWhiteSpace(blobName);

	private static string JoinName(string? first, string? middle, string? last) =>
		string.Join(" ", new[] { first, middle, last }.Where(x => !string.IsNullOrWhiteSpace(x)));

	private static EmployeeWorkAssignmentSummaryDto? Assignment(string? mode, Guid? yearId, string? yearName, Guid[] ids, string[] labels) =>
		mode == null ? null : new EmployeeWorkAssignmentSummaryDto
		{
			Mode = mode, AcademicYearId = yearId, AcademicYearName = yearName,
			OrganizationIds = ids.ToList(), OrganizationLabels = labels.ToList()
		};

	private static string AssignmentDisplay(string? mode, string? yearName, string[] labels, string? department)
	{
		var description = mode switch
		{
			EmployeeWorkAssignmentModes.SpecificOrganizations => $"{string.Join("; ", labels)} ({yearName})",
			EmployeeWorkAssignmentModes.SchoolWide => "School-wide / not tied to an academic organization",
			EmployeeWorkAssignmentModes.DecideLater => "Decide later",
			_ => string.IsNullOrWhiteSpace(department) ? "Not recorded" : $"Legacy department: {department}"
		};
		return description + (mode != null && !string.IsNullOrWhiteSpace(department) ? $"; Legacy department: {department}" : string.Empty);
	}
}
