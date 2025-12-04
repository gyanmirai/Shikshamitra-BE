using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// DTO for entrance exam student in list view
/// </summary>
public class EntranceExamStudentDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public string EducationBoard { get; set; }
	public string EducationMedium { get; set; }
	public string Standard { get; set; }
	public DateTime? ExamDate { get; set; }
	public string ExamTime { get; set; }
	public TimeSpan? ExamStartTime { get; set; }
	public TimeSpan? ExamEndTime { get; set; }
	public int? ExamRoomNo { get; set; }
	public decimal? TotalMarks { get; set; }
	public decimal? PassingMarks { get; set; }
	public decimal? ObtainedMarks { get; set; }
	public decimal? Percentage { get; set; }
	public decimal Progress { get; set; } // Enrollment progress percentage
	public EntranceExamStatusEnum? ExamStatus { get; set; }
	public string ExamStatusDisplay { get; set; }
	public StudentLifeCycleStatusEnum EnrollmentState { get; set; }
}
