using ShikshaMitra.HR.Employees;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeLeave : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public string LeaveType { get; private set; }
	public string Color { get; private set; }
	public bool HalfDay { get; private set; }
	public DateTimeOffset StartDate { get; private set; }
	public DateTimeOffset EndDate { get; private set; }
	public EmployeeLeaveStatusType Status { get; private set; }
	public string Description { get; private set; }
	public string? RejectionReason { get; private set; }
	
	public Employee? Employee { get; private set; }

	private EmployeeLeave() { } // For ORM

	internal EmployeeLeave(
		Guid id,
		Guid tenantId,
		string leaveType,
		string color,
		bool halfDay,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string description, string rejectionReason) : base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		Color = color;
		LeaveType = leaveType;
		HalfDay = halfDay;
		StartDate = startDate;
		EndDate = endDate;
		Description = description;
		RejectionReason = rejectionReason;
		Status = EmployeeLeaveStatusType.Pending;
	}

	internal EmployeeLeave UpdateLeaveStatus(EmployeeLeaveStatusType status, string? rejectionReason)
	{
		Status = status;
		RejectionReason = rejectionReason;
		return this;
	}
}