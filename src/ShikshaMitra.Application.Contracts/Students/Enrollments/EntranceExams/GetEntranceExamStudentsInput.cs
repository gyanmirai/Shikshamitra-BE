using ShikshaMitra.Students.StudentEnrollments;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Enrollments.EntranceExams;

/// <summary>
/// Input DTO for getting entrance exam students with filtering and pagination
/// </summary>
public class GetEntranceExamStudentsInput : PagedAndSortedResultRequestDto
{
	public DateTimeOffset? StartDate { get; set; }
	public DateTimeOffset? EndDate { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? EducationMediumId { get; set; }
	public EntranceExamStatusEnum? ExamStatus { get; set; }
	public string? SearchTerm { get; set; }
}
