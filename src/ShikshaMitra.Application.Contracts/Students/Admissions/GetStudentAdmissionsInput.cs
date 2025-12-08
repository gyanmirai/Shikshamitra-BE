using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Admissions;

/// <summary>
/// Input DTO for getting student admissions list with filters
/// </summary>
public class GetStudentAdmissionsInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public string? ApprovalStatus { get; set; }
	public Guid? DivisionId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? AcademicYearId { get; set; }
}
