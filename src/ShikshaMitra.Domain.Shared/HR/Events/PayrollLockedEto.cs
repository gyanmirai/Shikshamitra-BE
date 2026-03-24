using System;

namespace ShikshaMitra.HR.Events;

/// <summary>
/// Distributed event raised when payroll is locked for a period.
/// </summary>
public class PayrollLockedEto
{
	public int Month { get; set; }
	public int Year { get; set; }
	public int EmployeeCount { get; set; }
	public Guid? TenantId { get; set; }
	public DateTime LockedAt { get; set; }
}
