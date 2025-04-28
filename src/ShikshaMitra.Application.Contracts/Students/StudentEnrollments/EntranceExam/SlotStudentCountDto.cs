using System;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class SlotStudentCountDto
{
	public Guid EntranceExamSlotId { get; set; }
	public DateTimeOffset ExamDate { get; set; }
	public int StudentCount { get; set; }
}
