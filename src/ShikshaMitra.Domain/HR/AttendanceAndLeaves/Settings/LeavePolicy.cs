using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	
	public string LeaveType { get; private set; }
	public int LeaveDays { get; private set; }
	public bool CarryForward { get; private set; }
	public AccrualRate AccrualRate { get; private set; }
	public string Color { get; private set; }
	
	protected LeavePolicy() { } // For ORM
	
	internal LeavePolicy(Guid id, Guid? tenantId,string leaveType, int leaveDays, bool carryForward,
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