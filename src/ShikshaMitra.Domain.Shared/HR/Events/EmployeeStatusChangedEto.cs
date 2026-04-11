using System;

namespace ShikshaMitra.HR.Events;

/// <summary>
/// Distributed event raised after an employee's status changes.
/// </summary>
public class EmployeeStatusChangedEto
{
	public Guid EmployeeId { get; set; }
	public string? PreviousStatus { get; set; }
	public string CurrentStatus { get; set; } = default!;
	/// <summary>Audit trail: optional reason supplied when changing status.</summary>
	public string? ChangeReason { get; set; }
	public Guid? TenantId { get; set; }
	public DateTime ChangedAt { get; set; }
}
