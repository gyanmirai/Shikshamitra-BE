using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Detailed DTO for a student's entrance exam information
/// </summary>
public class EntranceExamDetailDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public string InquiryNo { get; set; }
	public string RegistrationFormNo { get; set; }
	public string EducationBoard { get; set; }
	public string EducationMedium { get; set; }
	public string Standard { get; set; }

	// Exam Schedule
	public bool ExamScheduled { get; set; }
	public Guid? EntranceExamSlotId { get; set; }
	public DateTime? ExamDate { get; set; }
	public TimeSpan? ExamStartTime { get; set; }
	public TimeSpan? ExamEndTime { get; set; }
	public int? ExamRoomNo { get; set; }

	// Exam Results
	public bool ResultUploaded { get; set; }
	public decimal? TotalMarks { get; set; }
	public decimal? PassingMarks { get; set; }
	public decimal? ObtainedMarks { get; set; }
	public decimal? Percentage { get; set; }
	public EntranceExamStatusEnum? ExamStatus { get; set; }
	public DateTime? ResultDate { get; set; }

	// Status
	public StudentLifeCycleStatusEnum EnrollmentState { get; set; }
	public bool CanMoveToAdmission { get; set; } // True if status is Pass
}
