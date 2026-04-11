using ShikshaMitra.Students.StudentEnrollments;
using System;

namespace ShikshaMitra.Students.Enrollments.EntranceExams;

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
	public DateTimeOffset? ExamStartTime { get; set; }
	public DateTimeOffset? ExamEndTime { get; set; }
	public int? ExamRoomNo { get; set; }
	public double? TotalMarks { get; set; }
	public double? PassingMarks { get; set; }
	public double? ObtainedMarks { get; set; }
	public double? Percentage { get; set; }
	public double Progress { get; set; } // Enrollment progress percentage
	public EntranceExamStatusEnum? ExamStatus { get; set; }
    public string ExamStatusDisplay { get; set; }
	public StudentLifeCycleStatusEnum EnrollmentState { get; set; }
}
