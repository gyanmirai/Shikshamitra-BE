using ShikshaMitra.Students.StudentEnrollments;
using System;

namespace ShikshaMitra.Students.Enrollments.EntranceExams;

/// <summary>
/// Response DTO after uploading entrance exam result
/// </summary>
public class EntranceExamResultResponseDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public double TotalMarks { get; set; }
	public double PassingMarks { get; set; }
	public double ObtainedMarks { get; set; }
	public double Percentage { get; set; }
	public EntranceExamStatusEnum ExamStatus { get; set; }
	public DateTime ResultDate { get; set; }
}
