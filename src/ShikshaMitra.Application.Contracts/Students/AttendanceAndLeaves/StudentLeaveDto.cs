using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students.StudentRecords;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class StudentLeaveDto : FullAuditedEntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public StudentDto Student { get; set; }
	public Guid LeaveTypeId { get; set; }
	public LeavePolicyDto LeavePolicy { get; set; }
	public bool HalfDay { get; set; }
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
	public EmployeeLeaveStatusType Status { get; set; }
	public string? Description { get; set; }
	public string? RejectionReason { get; set; }

	// Attachment
	public byte[]? Attachment { get; set; }

}