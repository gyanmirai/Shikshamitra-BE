using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicy : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	
	public string LeaveType { get; set; }
	public int LeaveDays { get; set; }
	public bool CarryForward { get; set; }
	public AccrualRate AccrualRate { get; set; }
	public string Color { get; set; }
	
	protected LeavePolicy() { } // For ORM
	
	public LeavePolicy(Guid id, Guid? tenantId,string leaveType, int leaveDays, bool carryForward,
		 AccrualRate accrualRate, string color) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		LeaveType = leaveType;
		LeaveDays = leaveDays;
		CarryForward = carryForward;
		AccrualRate = accrualRate;
		Color = color;
	}

}