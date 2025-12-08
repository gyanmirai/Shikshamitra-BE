using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Response DTO after uploading entrance exam result
/// </summary>
public class EntranceExamResultResponseDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public decimal TotalMarks { get; set; }
	public decimal PassingMarks { get; set; }
	public decimal ObtainedMarks { get; set; }
	public decimal Percentage { get; set; }
	public EntranceExamStatusEnum ExamStatus { get; set; }
	public DateTime ResultDate { get; set; }
}
