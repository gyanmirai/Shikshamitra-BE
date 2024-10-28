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

	public EmployeeAttendanceStatusType StatusType { get; set; }

	public double? TotalHoursWorked
	{
		get
		{
			switch (StatusType)
			{
				case EmployeeAttendanceStatusType.FullDayLeave:
					return 0.0;
				case EmployeeAttendanceStatusType.HalfDayLeave:
					return 4.0;
				case EmployeeAttendanceStatusType.Present:
					if (!TimeIn.HasValue || !TimeOut.HasValue)
					{
						return null;
					}

					var timeIn = TimeIn.Value.ToTimeSpan();
					var timeOut = TimeOut.Value.ToTimeSpan();

					return Math.Round((timeOut - timeIn).TotalHours, 2);

				default:
					return null;
			}
		}
	}

	// Navigation properties
	public virtual Employee Employee { get; set; }
}