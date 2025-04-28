using ShikshaMitra.Students.StudentRecords;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

public class StudentAttendance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid StudentId { get; set; }
	public DateTimeOffset AttendanceDate { get; set; }
	public bool IsPresent { get; set; }

	public Guid? TenantId { get; }

	public string? Remarks { get; set; }

	// Navigation Propery
	public Student Student { get; set; }


	private StudentAttendance() { }

	public StudentAttendance(Guid id, Guid tenantId, Guid studentId, DateTimeOffset attendanceDate, bool isPresent, string? remarks)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		AttendanceDate = attendanceDate;
		IsPresent = isPresent;
		Remarks = remarks;
	}
}
