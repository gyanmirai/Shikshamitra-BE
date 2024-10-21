using ShikshaMitra.HR.AttendanceAndLeaves;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAttendance : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid EmployeeId { get; set; }
	public DateOnly AttendanceDate { get; set; }
	public TimeOnly? TimeIn { get; set; }
	public TimeOnly? TimeOut { get; set; }
	public EmployeeAttendanceStatus Status { get; set; }
	public double? TotalHoursWorked { get; set; }
	// Navigation properties
	public virtual Employee Employee { get; set; }
}