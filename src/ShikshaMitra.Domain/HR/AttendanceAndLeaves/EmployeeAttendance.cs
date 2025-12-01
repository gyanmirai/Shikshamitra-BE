using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.Security.UserScope;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class EmployeeAttendance : FullAuditedEntity<Guid>, IMultiTenant, IUserScopedEntity
{
	public Guid? TenantId { get; set; }
	public Guid EmployeeId { get; set; }

	// Store only TimeIn and TimeOut as DateTimeOffset values
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }

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

					var duration = TimeOut.Value - TimeIn.Value;
					return Math.Round(duration.TotalHours, 2);

				default:
					return null;
			}
		}
	}

	public virtual Employee Employee { get; set; }

	#region IUserScopedEntity Explicit Implementation
	// Delegate to Employee's EducationMediumStage scope
	// Employee attendance is not filtered by academic year
	Guid? IUserScopedEntity.AcademicYearId => null;
	Guid? IUserScopedEntity.EducationBoardId => null; // Employee doesn't have board/medium
	Guid? IUserScopedEntity.EducationMediumId => null;
	Guid? IUserScopedEntity.EducationMediumStageId => Employee?.EducationMediumStageId;
	Guid? IUserScopedEntity.StandardId => null;
	Guid? IUserScopedEntity.DivisionId => null;
	#endregion
}