using System;

namespace ShikshaMitra.HR.Events;

/// <summary>
/// Distributed event raised when a leave request status changes.
/// </summary>
public class LeaveStatusChangedEto
{
	public Guid LeaveId { get; set; }
	public Guid EmployeeId { get; set; }
	public string PreviousStatus { get; set; } = default!;
	public string NewStatus { get; set; } = default!;
	public Guid? TenantId { get; set; }
	public DateTime ChangedAt { get; set; }
}
