using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class EntranceExamDto :  AuditedEntityDto<Guid>
{
	public Guid EntranceExamSlotId { get; set; }
	public EntranceExamSlotDto EntranceExamSlot { get; set; }
	public Guid StudentEnrollmentId { get; set; }
	public StudentEnrollmentDto StudentEnrollment { get; set; }
	public DateTimeOffset ExamDate { get; set; }
	public float? PassingMark { get; set; }
	public float? MarksObtained { get; set; }
	public float? TotalMarks { get; set; }
}