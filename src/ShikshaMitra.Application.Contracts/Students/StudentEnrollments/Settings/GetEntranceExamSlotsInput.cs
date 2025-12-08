using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class GetEntranceExamSlotsInput : PagedAndSortedResultRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public string? SearchTerm { get; set; }
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
}
