using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// DTO for filtering student leave list requests
/// </summary>
public class GetStudentLeaveListInput : PagedAndSortedResultRequestDto
{
	public Guid? BoardId { get; set; }
	public string? FilterText { get; set; }
	public string? TableFilters { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? EducationMediumId { get; set; }
	public Guid? OrganizationId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public Guid? StudentLeaveTypeId { get; set; }
	public StudentLeaveStatusEnum? Status { get; set; }
	public DateTimeOffset? StartDate { get; set; }
	public DateTimeOffset? EndDate { get; set; }
}
