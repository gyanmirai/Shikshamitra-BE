using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class GetAvailableExamSlotsInput : PagedAndSortedResultRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public DateTime? Date { get; set; }
	public int? MinAvailableSeats { get; set; }
}
