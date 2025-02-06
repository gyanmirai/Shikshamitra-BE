using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Students.Attendance;

public class StudentAttendance : FullAuditedAggregateRoot<Guid>
{
	public Guid StudentId { get; set; }
	public DateTimeOffset AttendanceDate { get; set; }
	public bool IsPresent { get; set; }


	//private StudentAttendance() { }

	public StudentAttendance(Guid id, Guid studentId, DateTimeOffset attendanceDate, bool isPresent)
		: base(id)
	{
		StudentId = studentId;
		AttendanceDate = attendanceDate;
		IsPresent = isPresent;
	}
}
