using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Attendance;

public class StudentLeave : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid LeaveTypeId { get; private set; }  // Foreign key to LeavePolicy
	public bool HalfDay { get; private set; }
	public DateTimeOffset StartDate { get; private set; }
	public DateTimeOffset EndDate { get; private set; }
	public EmployeeLeaveStatusType Status { get; private set; }
	public string Description { get; private set; }
	public string? RejectionReason { get; private set; }
	public Student? Student { get; private set; }
	public LeavePolicy? LeavePolicy { get; private set; } // Navigation property

	// Attachment
	public byte[]? Attachment { get; set; }

	private StudentLeave() { } // For ORM

	internal StudentLeave UpdateLeaveStatus(EmployeeLeaveStatusType status, string? rejectionReason)
	{
		Status = status;
		RejectionReason = rejectionReason;
		return this;
	}
}
