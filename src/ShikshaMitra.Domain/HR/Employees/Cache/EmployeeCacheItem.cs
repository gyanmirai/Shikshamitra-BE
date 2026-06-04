using System;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Cache item for employee data with all auto-included navigations.
/// </summary>
[Serializable]
public class EmployeeCacheItem
{
	public Guid Id { get; set; }
	public Guid? TenantId { get; set; }
	public Guid? UserId { get; set; }
	public string EmployeeCode { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? Email { get; set; }
	public string? MobileNo { get; set; }
	public Guid RoleId { get; set; }
	public string? RoleName { get; set; }
	public Guid? ShiftId { get; set; }
	public string? ShiftName { get; set; }
	public EmployeeStatusType? EmployeeStatusType { get; set; }
	public DateTime CachedAt { get; set; }
}
