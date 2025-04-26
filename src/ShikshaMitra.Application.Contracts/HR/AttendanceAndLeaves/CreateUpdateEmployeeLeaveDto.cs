using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class CreateUpdateEmployeeLeaveDto
{
	[Required] public Guid EmployeeId { get; set; }
	
	[Required] public Guid  LeaveTypeId { get; set; } // Reference to LeavePolicy
	
	[Required] public bool HalfDay { get; set; }

	[Required] public DateTimeOffset StartDate { get; set; }
	
	public DateTimeOffset EndDate { get; set; }

	public string? Description { get; set; }
	
	// Attachment
	public byte[]? Attachment { get; set; }
	
}