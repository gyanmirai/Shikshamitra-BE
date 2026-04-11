using System;

namespace ShikshaMitra.HR.Events;

/// <summary>
/// Distributed event raised after a new employee is created.
/// </summary>
public class EmployeeCreatedEto
{
	public Guid EmployeeId { get; set; }
	public string EmployeeCode { get; set; } = default!;
	public string FullName { get; set; } = default!;
	public string? Email { get; set; }
	public Guid? TenantId { get; set; }
	public DateTime CreatedAt { get; set; }
}
