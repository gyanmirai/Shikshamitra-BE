using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentRecords;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

public class StudentAttendance : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
	public Guid StudentId { get; set; }
	public DateTimeOffset AttendanceDate { get; set; }
	public bool IsPresent { get; set; }
	public Guid? TenantId { get; }
	public string? Remarks { get; set; }

	// Navigation Property
	public Student Student { get; set; }

	#region IUserScopedEntity Explicit Implementation
	// Delegate to Student's scope properties
	Guid? IUserScopedEntity.EducationBoardId => Student?.EducationBoardId;
	Guid? IUserScopedEntity.EducationMediumId => Student?.EducationMediumId;
	Guid? IUserScopedEntity.EducationMediumStageId => Student?.EducationMediumStageId;
	Guid? IUserScopedEntity.StandardId => Student?.StandardId;
	Guid? IUserScopedEntity.DivisionId => Student?.DivisionId;
	#endregion

	private StudentAttendance() { }

	public StudentAttendance(
		Guid id,
		Guid tenantId,
		Guid studentId,
		DateTimeOffset attendanceDate,
		bool isPresent,
		string? remarks)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		AttendanceDate = attendanceDate;
		IsPresent = isPresent;
		Remarks = remarks;
	}
}