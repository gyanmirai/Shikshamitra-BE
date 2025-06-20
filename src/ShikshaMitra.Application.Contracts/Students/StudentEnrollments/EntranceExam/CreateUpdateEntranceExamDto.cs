using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class CreateUpdateEntranceExamDto
{
	[Required] public Guid EntranceExamSlotId { get; set; }
	[Required] public Guid StudentEnrollmentId { get; set; }
	[Required] public DateTimeOffset ExamDate { get; set; }
	public float? PassingMark { get; set; }

	public float? MarksObtained { get; set; }
	public float? TotalMarks { get; set; }
}