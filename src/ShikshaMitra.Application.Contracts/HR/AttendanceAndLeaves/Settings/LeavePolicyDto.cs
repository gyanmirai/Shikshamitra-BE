using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyDto : AuditedEntityDto<Guid>
{
	public Guid? TenantId { get; set; }
	public string LeaveType { get; set; }
	public int LeaveDays { get; set; }
	public bool CarryForward { get; set; }
	public AccrualRate AccrualRate { get; set; }
	public string Color { get; set; }
}