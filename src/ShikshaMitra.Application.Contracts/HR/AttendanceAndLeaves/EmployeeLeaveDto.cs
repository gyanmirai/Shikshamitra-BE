using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeLeaveDto : FullAuditedEntityDto<Guid>
{
	public Guid UserId { get; set; }
	public Guid EmployeeId { get; set; }
	public string LeaveType { get; set; }
	public string Color { get; set; }
	public bool HalfDay { get; set; }
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
	public EmployeeLeaveStatusType Status { get; set; }
	public string Description { get; set; }
	public string? RejectionReason { get; set; }
}