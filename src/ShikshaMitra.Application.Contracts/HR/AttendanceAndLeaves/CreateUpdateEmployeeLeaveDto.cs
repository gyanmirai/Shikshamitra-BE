using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class CreateUpdateEmployeeLeaveDto
{
	[Required] public Guid EmployeeId { get; set; }
	
	[Required] public Guid  LeaveTypeId { get; set; } // Reference to LeavePolicy
	
	[Required] public string Color { get; set; }

	[Required] public bool HalfDay { get; set; }

	[Required] public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }

	[Required] public string Description { get; set; }
}