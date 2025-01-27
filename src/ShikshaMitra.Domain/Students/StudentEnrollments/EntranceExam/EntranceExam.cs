using ShikshaMitra.Students.EntranceExam;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class EntranceExam :  FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EntranceExamSlotId { get; private set; }
	public Guid StudentEnrollmentId { get; private set; }
	public DateTimeOffset ExamDate { get; private set; }
	public string ExamType { get; private set; }
	public float TotalMarks { get; private set; }
	public ExamScheduleType ExamScheduleType { get; private set; }
	public EntranceExamSlot EntranceExamSlot { get; private set; }
	public StudentEnrollment StudentEnrollment { get; private set; }

	protected EntranceExam() {}

	internal EntranceExam(Guid id, Guid tenantId, Guid entranceExamSlotId, Guid studentEnrollmentId, DateTimeOffset examDate, string examType,
		float totalMarks, ExamScheduleType examScheduleType) : base(id)
	{
		TenantId = tenantId;
		EntranceExamSlotId = entranceExamSlotId;
		StudentEnrollmentId = studentEnrollmentId;
		ExamDate = examDate;
		ExamType = examType;
		TotalMarks = totalMarks;
		ExamScheduleType = examScheduleType;
	}

}