using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeLeave : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Stable, auto-increment public ID used for API routes and display.
	/// DB-generated identity to ensure uniqueness and scalability.
	/// </summary>
	public int PublicId { get; private set; }

	public Guid EmployeeId { get; private set; }
	public Guid LeaveTypeId { get; private set; }  // Foreign key to LeavePolicy
	public bool HalfDay { get; private set; }
	public DateTimeOffset StartDate { get; private set; }
	public DateTimeOffset EndDate { get; private set; }
	public EmployeeLeaveStatusType Status { get; private set; }
	public LeavePaidType? ApprovedPaidType { get; private set; }
	public decimal ApprovedLeaveUnits { get; private set; }
	public string? Description { get; private set; }
	public string? RejectionReason { get; private set; }
	public Employee? Employee { get; private set; }
	public LeavePolicy? LeavePolicy { get; private set; } // Navigation property

	// Attachment
	public byte[]? Attachment { get; set; }

	private EmployeeLeave() { } // For ORM

	internal EmployeeLeave(
		Guid id,
		Guid tenantId,
		bool halfDay,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string description, string rejectionReason) : base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
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

	internal EmployeeLeave SetApprovalPayrollSnapshot(LeavePaidType paidType, decimal approvedLeaveUnits)
	{
		if (approvedLeaveUnits < 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveNoBusinessDaysInRange);
		}

		ApprovedPaidType = paidType;
		ApprovedLeaveUnits = approvedLeaveUnits;
		return this;
	}

	/// <summary>
	/// Soft-cancel: transitions to Cancelled status with audit retention.
	/// Only allowed from Pending or Reviewed states.
	/// </summary>
	internal EmployeeLeave Cancel()
	{
		if (Status != EmployeeLeaveStatusType.Pending && Status != EmployeeLeaveStatusType.Reviewed)
		{
			throw new BusinessException(HRErrorCodes.LeaveNotCancellable);
		}

		Status = EmployeeLeaveStatusType.Cancelled;
		return this;
	}

	/// <summary>Factory method to create EmployeeLeave with proper initialization.</summary>
	public static EmployeeLeave Create(
		Guid id,
		Guid tenantId,
		Guid employeeId,
		Guid leaveTypeId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string description,
		bool halfDay = false)
	{
		var leave = new EmployeeLeave(id, tenantId, halfDay, startDate, endDate, description, null!);

		// Use reflection only in controlled factory method to set private properties
		var employeeIdProperty = typeof(EmployeeLeave).GetProperty(nameof(EmployeeId));
		var leaveTypeIdProperty = typeof(EmployeeLeave).GetProperty(nameof(LeaveTypeId));

		employeeIdProperty?.SetValue(leave, employeeId);
		leaveTypeIdProperty?.SetValue(leave, leaveTypeId);

		return leave;
	}
}
