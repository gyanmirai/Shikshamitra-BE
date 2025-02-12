using ShikshaMitra.Students.EntranceExam;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class CreateUpdateEntranceExamDto
{
	[Required] public Guid EntranceExamSlotId { get; set; }
	[Required] public Guid StudentEnrollmentId { get; set; }
	[Required] public DateTimeOffset ExamDate { get; set; }
	[Required] public string ExamType { get; set; }
	public float? MarksObtained { get; set; }
	[Required] public float TotalMarks { get; set; }
	[Required] public ExamScheduleType ExamScheduleType { get; set; }
}