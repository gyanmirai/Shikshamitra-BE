using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class GetEntranceExamSlotsInput : PagedAndSortedResultRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public string? SearchTerm { get; set; }
	public DateTimeOffset? StartDateTime { get; set; }
	public DateTimeOffset? EndDateTime { get; set; }
}
