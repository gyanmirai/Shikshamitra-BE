using ShikshaMitra.Students.StudentEnrollments;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Enrollments.Admissions;

/// <summary>
/// Input DTO for getting student admissions list with filters
/// </summary>
public class GetStudentAdmissionsInput : PagedAndSortedResultRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public string? Search { get; set; }
	public StudentEnrollmentApprovalState ApprovalStatus { get; set; } = StudentEnrollmentApprovalState.Approved;
	public Guid? DivisionId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? AcademicYearId { get; set; }
}
